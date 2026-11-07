# Story Framework

本文档描述 `src/Story` 当前实际使用的剧情框架，供后续开发和其他 AI 参考。

## 1. 设计目标

```text
节点 = 暂停状态，只提供玩家选项
边   = 一轮剧情注册，注册完由 StoryPlayer 执行
对话 = 由原版 Dialogue 链推进
事件 = 由边注册，在指定台词结束后启动
```

核心原则：

```text
1. 不使用反射自动发现剧情，剧情在 Plan 中显式登记
2. 边只注册命令，不 await 事件、不手动推进下一句
3. 节点不持有 NPC 台词，NPC 台词属于边
4. 每个事件有独立的完成标识
5. 原版 Dialogue.nextDialogue 负责逐句推进
6. StoryPlayer 只负责监控本轮结束、推进节点或退出
```

## 2. 目录结构

```text
src/Story/
├─ README.md
├─ Runtime/
│  ├─ StoryPlayer.cs              统筹入口
│  ├─ README.md
│  ├─ Contracts/
│  │  ├─ StoryAssembly.cs
│  │  ├─ NpcStoryAssembly.cs
│  │  ├─ StoryPhase.cs
│  │  └─ README.md
│  ├─ Graph/
│  │  ├─ StoryNode.cs
│  │  ├─ StoryEdge.cs
│  │  └─ README.md
│  ├─ Execution/
│  │  ├─ StorySequence.cs
│  │  └─ README.md
│  └─ Dialogue/
│     ├─ StoryNodeDialogue.cs
│     └─ README.md
└─ Npcs/
   └─ FlowEmitterMerchant/
      ├─ FlowEmitterMerchantNpc.cs
      ├─ FlowEmitterMerchantPlan.cs
      ├─ FlowEmitterMerchantProgress.cs
      ├─ FlowEmitterMerchantStock.cs
      └─ Events/
         ├─ FlowEmitterDemo/
         ├─ FlowEmitterSecondVisit/
         ├─ FlowEmitterGenericVisit/
         └─ FlowEmitterFarewell/
```

`Runtime` 是通用框架，`Npcs/<Npc>` 是具体 NPC 的剧情、文本、节点连接和库存策略。

## 3. 核心类型

### StoryAssembly

```csharp
internal abstract class StoryAssembly
{
    internal abstract StoryEdge Entry { get; }

    internal virtual void Complete()
    {
    }
}
```

只描述“从哪里开始”和“完成后做什么”。

### NpcStoryAssembly

```csharp
internal abstract class NpcStoryAssembly : StoryAssembly
{
    internal abstract StoryPhase Phase { get; }

    internal abstract bool CanPlay();
}
```

新增自定义阶段时扩展 `StoryPhase`：

```csharp
internal enum StoryPhase
{
    Arrival,
    SecondVisit,
    GenericVisit,
    SoldOut,
}
```

### StoryNode

节点只保存玩家选项：

```csharp
internal sealed class StoryNode
{
    internal IReadOnlyList<StoryEdge> Outgoing { get; }
}
```

节点没有 Prompt，没有 NPC 文本。

### StoryEdge

```csharp
internal abstract class StoryEdge
{
    internal string Label { get; }
    internal string IconItemKey { get; }
    internal StoryNode To { get; }

    internal abstract Task Execute(StorySequence sequence);

    internal void Connect(StoryNode from, StoryNode to);
}
```

一条边就是一轮注册：

```csharp
internal override Task Execute(StorySequence player)
{
    player.SayNpc("你好。");
    player.StartFlow(FlowEventApi.RunBoundaryEmitter);
    player.Do(delegate { /* 同步动作 */ });
    return Task.CompletedTask;
}
```

规则：

```text
不写 await
不等待事件
不调用下一句
只注册台词、事件、同步动作
```

## 4. 执行流程

```text
NPC OnArrival
→ FlowEmitterMerchantNpc.StartStory
→ TryPlay(Phase)
→ new StoryPlayer(grid, npcName, onExit).Start(assembly)
→ StoryPlayer.RunEdge(assembly.Entry, null)
→ StorySequence 创建
→ edge.Execute(sequence) 注册命令
→ sequence.Run(node, RunOption) 执行
→ 等待最后一句台词 + 所有 Flow
→ To == null ? ExitStory : StoryNodeDialogue
```

### 4.1 一轮注册

`StorySequence` 保存本轮注册结果：

```csharp
player.SayNpc(...)        // 追加 Dialogue
player.SayPlayer(...)     // 追加玩家台词
player.StartFlow(...)     // 在当前位置注册事件
player.Do(...)            // 在当前位置注册同步动作
```

`SayNpc` 内部直接串 `nextDialogue`：

```csharp
if (_first == null)
{
    _first = line;
}
else
{
    _tail.SetNextDialogue(line);
}

_tail = line;
```

`StoryPlayer` 不逐句调用下一句，原版负责：

```text
Dialogue.nextDialogue
→ DialogUIManager.ContinueConversation()
```

### 4.2 Flow 的启动位置

`StartFlow` 挂在“当前最后一句台词”的 `endAction` 上：

```text
Say intro
Say flowName
StartFlow(boundary)   ← 在 flowName 结束后启动
Say relay
StartFlow(relay)      ← 在 relay 结束后启动
```

如果没有前置台词：

```csharp
player.StartFlow(...)
```

则立即启动事件。

多个 Flow 用 `_flowTail` 串行：

```text
Boundary 完成
→ 启动 Relay
```

### 4.3 对话链末尾

`StorySequence.Run` 会：

```text
1. 如果没有 prefix，StartDialogue(first)
2. 在最后一句挂完成 TCS
3. await 最后一句
4. await 所有 Flow
5. 返回 attached 状态
```

真正逐句播放仍然是原版，`Run` 只启动一次并等待整轮结束。

### 4.4 节点选项

没有 Flow 的边：

```text
最后一句 NPC 台词
→ 节点选项直接挂在这句上
→ 同一个 Dialogue 链，不重建
```

有 Flow 的边：

```text
主对话链先播
→ 等 Flow 完成
→ 再播放最后一句 NPC 台词
→ 节点选项挂在这句上
```

这样避免事件播放期间出现空白选项节点。

### 4.5 玩家选择

`StoryNodeDialogue` 负责：

```text
Dialogue label   选项气泡文本
Dialogue selected 实际的玩家台词
label.nextDialogue = selected
selected.startAction → player.RunOption(edge, selected)
```

选中后：

```text
RunOption(edge, selected)
→ new StorySequence(grid, npcName, selected)
→ edge.Execute(sequence)
→ 继续使用同一条 selected 对话链
```

`selected` 已经在播放，因此不会重新 `StartDialogue`。

## 5. 事件层边界

事件层入口：

```csharp
FlowEventApi.RunBoundaryEmitter
FlowEventApi.RunTwoEmitterRelay
FlowEventApi.RunFilterLoop
FlowEventApi.RunColdTrapLoop
```

事件完成由事件自身决定：

```csharp
session.Complete();
```

`EventSession.Complete()` 会：

```text
停止协程
执行 cleanup
销毁演示物品
调用 FlowEventApi.EventCompleted(grid)
```

`StorySequence.StartFlowNow` 监听：

```csharp
FlowEventApi.EventCompleted += handler;
FlowEventApi.LockGrid(grid);

if (!flow(grid))
{
    // 启动失败，立即完成
}
```

所以 Story 层只监听 `EventCompleted`，不轮询事件层的 Active 状态。

事件没有调用 `Complete()` 时，剧情会一直等待；结束条件由事件层负责。

## 6. 标识设计

所有进度都在 `FlowEmitterMerchantProgress`，每个事件独立：

```text
ArrivalSeenKey    第一次到访剧情已播放
FirstStockKey     首次破旧库存已上架
FirstStoryKey     第一次剧情已完成
SecondVisitKey    第二次到访剧情已完成
FarewellKey       赠礼剧情已完成
VisitCountKey     到访次数
ThirdVisitKey     第三次到访
```

演示/提示进度：

```text
EmitterDemoKey
FilterDemoKey
ColdTrapDemoKey
SoundHintKey
```

### 关键约束

```text
FirstStoryKey 在 FlowEmitterDemoAssembly.Complete 写入
FarewellKey   在 FlowEmitterFarewellAssembly.Complete 写入
```

赠礼是否发生不再影响第一次剧情是否完成。

```text
FirstStoryKey  → 第一次剧情完成
FarewellKey    → 赠礼完成
```

这样即使玩家没有买完第一批货：

```text
第一次剧情仍算完成
第二次到访仍能触发
赠礼继续等待货物卖完
```

库存规则只使用剧情级标识：

```csharp
FirstStoryKey  → flow_emitter ×2 + flow_filter ×1
SecondVisitKey → flow_cold_trap ×1
ThirdVisitKey  → flow_straight_speed ×2
                 l_turn_device ×2
                 flow_straight ×4
```

首次破旧库存使用自己的 `FirstStockKey`，不依赖 `ArrivalSeenKey`。

## 7. NPC 到访生命周期

`FlowEmitterMerchantNpc.StartStory`：

```text
IncrementVisitCount()

TryPlay(Arrival)
→ TryPlay(SecondVisit)
→ TryPlay(GenericVisit)
→ OpenShop()
```

`OnStoryFinished`：

```text
Arrival      → FinishStory()
SecondVisit  → OpenShop()
GenericVisit → OpenShop()
SoldOut      → CurrentClientLeave()
```

`FinishStory` / `OpenShop` 都会在满足条件时：

```text
绑定 onItemSold
→ TryStartFarewell()
```

所以赠礼可以在后续到访卖空时发生，不要求第一次到访当场买完。

## 8. 新增剧情步骤

### 8.1 建目录

```text
src/Story/Npcs/<Npc>/Events/<StoryName>/
├─ <StoryName>Texts.cs
├─ <StoryName>Assembly.cs
└─ Edges/
   ├─ XxxGreetingEdge.cs
   ├─ XxxChoiceEdge.cs
   └─ XxxExitEdge.cs
```

### 8.2 写文本

```csharp
internal static class MyStoryTexts
{
    internal const string Greeting = "你好。";
    internal const string Option = "可以。";
}
```

文本不包含 NPC 名字或玩家名字。

### 8.3 写边

```csharp
internal sealed class MyGreetingEdge : StoryEdge
{
    internal MyGreetingEdge()
        : base(null, null)
    {
    }

    internal override Task Execute(StorySequence player)
    {
        player.SayNpc(MyStoryTexts.Greeting);
        return Task.CompletedTask;
    }
}
```

带事件的边：

```csharp
internal override Task Execute(StorySequence player)
{
    player.SayNpc(MyStoryTexts.DemoIntro);
    player.StartFlow(FlowEventApi.RunColdTrapLoop);

    player.SayNpc(MyStoryTexts.AfterDemo);
    player.SayNpc(MyStoryTexts.RepeatPrompt);

    return Task.CompletedTask;
}
```

不要在边里：

```csharp
await player.StartFlow(...);
await player.WaitForSpeech();
```

### 8.4 写装配类

```csharp
internal sealed class MyStoryAssembly : NpcStoryAssembly
{
    internal override StoryEdge Entry { get; }

    internal override StoryPhase Phase
    {
        get { return StoryPhase.Arrival; }
    }

    internal override bool CanPlay()
    {
        return !MyProgress.HasPlayed;
    }

    internal override void Complete()
    {
        MyProgress.MarkPlayed();
    }

    internal MyStoryAssembly()
    {
        StoryNode root = new StoryNode();

        MyGreetingEdge greeting = new MyGreetingEdge();
        MyChoiceEdge choice = new MyChoiceEdge();
        MyExitEdge exit = new MyExitEdge();

        greeting.Connect(null, root);
        choice.Connect(root, root);
        exit.Connect(root, null);

        Entry = greeting;
    }
}
```

### 8.5 登记到 Plan

```csharp
private static readonly NpcStoryAssembly[] Stories =
{
    new FlowEmitterDemoAssembly(),
    new FlowEmitterSecondVisitAssembly(),
    new FlowEmitterGenericVisitAssembly(),
    new FlowEmitterFarewellAssembly(),
};

internal static NpcStoryAssembly[] CreateStories()
{
    return Stories;
}
```

Plan 是进程内静态缓存；Assembly/Node/Edge 在构造时建图，运行时不再修改图结构。

## 9. 常见错误

### 错误 1：在节点里放 NPC 台词

```csharp
new StoryNode("目前有这些货。")   // 错误
```

正确：

```text
NPC 台词写在边里
节点只保留 Outgoing
```

### 错误 2：边里 await 事件

```csharp
await player.StartFlow(...);   // 错误
```

后面的台词不会入队，事件结束时对话可能已经结束。

正确：

```csharp
player.StartFlow(...);

player.SayNpc(...);
```

### 错误 3：用演示 key 决定库存

```text
EmitterDemoKey → 发射器
FilterDemoKey  → 过滤装置
```

错误。库存应该由剧情级标识决定：

```text
FirstStoryKey
SecondVisitKey
ThirdVisitKey
```

### 错误 4：把 FirstStoryKey 绑在赠礼上

```text
赠礼发生 → FirstStoryKey
```

错误。赠礼和剧情完成是两个事件，分别使用：

```text
FirstStoryKey
FarewellKey
```

### 错误 5：手动推进对话

不要在 Story 层调用：

```csharp
DialogUIManager.ContinueConversation()
```

原版 `Dialogue.nextDialogue` 已经负责逐句推进。

## 10. 性能注意

标识读取：

```text
Dictionary<string,string>.ContainsKey
≈ 十几 ns/次
```

只发生在：

```text
到访
CanPlay
OpenShop
OnItemSold
剧情完成
库存 Place
```

没有每帧读取。

已优化：

```text
剧情目录静态缓存
→ 一次进程只创建一次 Assembly/Node/Edge 图
```

主要耗时在 GameItem 创建、库存上架和事件演示，不在标识读取。

## 11. 参考实现

当前完整示例：

```text
src/Story/Npcs/FlowEmitterMerchant/
```

包括：

```text
第一次到访：演示 + 首次破旧库存 + 赠礼
第二次到访：冷萃装置演示
通用到访：普通招呼
离场：赠礼与每 7 天提醒
库存：剧情级规则 + 第三次额外小玩意
```
