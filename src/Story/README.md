# Story Framework

`src/Story` 是纯对话剧情框架，不再负责装置演示，也不依赖 `src/Events`。

## 1. 核心模型

```text
节点 = 暂停状态，只提供玩家选项
边   = 一轮剧情注册，注册完由 StoryPlayer 执行
对话 = 由原版 Dialogue 链推进
```

核心原则：

```text
1. 不使用反射自动发现剧情，剧情在 Plan 中显式登记
2. 边只注册台词和同步动作，不等待外部系统
3. 节点不持有 NPC 台词，NPC 台词属于边
4. 原版 Dialogue.nextDialogue 负责逐句推进
5. StoryPlayer 只负责监控本轮结束、推进节点或退出
```

剧情不再调用 `FlowEventApi`，也不锁网格或等待装置演示。

## 2. 目录

```text
src/Story/
├─ Runtime/
│  ├─ StoryPlayer.cs
│  ├─ Contracts/
│  │  ├─ StoryAssembly.cs
│  │  ├─ NpcStoryAssembly.cs
│  │  └─ StoryPhase.cs
│  ├─ Graph/
│  │  ├─ StoryNode.cs
│  │  └─ StoryEdge.cs
│  ├─ Execution/
│  │  └─ StorySequence.cs
│  ├─ Dialogue/
│  │  └─ StoryNodeDialogue.cs
│  └─ Portraits/
│     └─ NpcPortraitRegistry.cs
└─ Npcs/
   └─ FlowEmitterMerchant/
      ├─ FlowEmitterMerchantNpc.cs
      ├─ FlowEmitterMerchantPlan.cs
      ├─ FlowEmitterMerchantProgress.cs
      ├─ FlowEmitterMerchantStock.cs
      └─ Events/
         ├─ FlowEmitterArrival/      首次到访（纯对话）
         ├─ FlowEmitterSecondVisit/  第二次到访（纯对话）
         ├─ FlowEmitterGenericVisit/ 通用到访
         └─ FlowEmitterFarewell/     卖完后的离场
```

## 3. 核心类型

### StoryAssembly

```csharp
internal abstract class StoryAssembly
{
    internal abstract StoryEdge Entry { get; }
    internal virtual void Complete() { }
}
```

### NpcStoryAssembly

```csharp
internal abstract class NpcStoryAssembly : StoryAssembly
{
    internal abstract StoryPhase Phase { get; }
    internal abstract bool CanPlay();
}
```

### StoryNode

```csharp
internal sealed class StoryNode
{
    internal IReadOnlyList<StoryEdge> Outgoing { get; }
}
```

节点只保存选项，不保存 NPC 提示。

### StoryEdge

```csharp
internal abstract class StoryEdge
{
    internal string Label { get; }
    internal string IconItemKey { get; }
    internal StoryNode From { get; }
    internal StoryNode To { get; }

    internal abstract Task Execute(StorySequence sequence);
    internal void Connect(StoryNode from, StoryNode to);
}
```

边只注册：

```text
SayNpc / SayPlayer  台词
Do                 同步动作
```

## 4. 执行流程

```text
NPC OnArrival
→ FlowEmitterMerchantNpc.StartStory
→ TryPlay(Phase)
→ new StoryPlayer(npcName, onExit).Start(assembly)
→ StoryPlayer.RunEdge(assembly.Entry, null)
→ edge.Execute(sequence) 注册台词和动作
→ sequence.Run(node, RunOption)
→ 等待最后一句台词
→ To == null ? ExitStory : 显示节点选项
```

选项被选中后：

```text
selected.startAction
→ StoryPlayer.RunOption
→ RunEdge(option, selected)
```

没有网格锁、空桌检查或事件等待。

## 5. 对话链

`StorySequence.SayNpc` / `SayPlayer` 直接串：

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

最后一句的 `endAction` 触发本轮完成。`Do` 把同步动作挂到当前最后一句。

## 6. NPC 剧情

`FlowEmitterMerchantPlan` 显式登记：

```text
FlowEmitterArrivalAssembly         首次到访
FlowEmitterSecondVisitAssembly  第二次到访
FlowEmitterGenericVisitAssembly 通用到访
FlowEmitterFarewellAssembly     卖完后的离场
```

每个装载类自己声明：

```text
Phase      阶段
CanPlay()  前置条件
Complete() 完成标识
```

新增剧情：

```text
1. 写 StoryAssembly
2. 在 FlowEmitterMerchantPlan 的 Stories 列表加一行
```

## 7. 边界

```text
Story 只负责对话、选项、节点推进
不负责装置演示、事件会话、网格锁
装置演示和事件编排属于 src/Events，由其他系统调用
```

剧情不引用：

```text
src/Events
FlowEventApi
EventSession
GridLock
```

新增 NPC 时在 `src/Story/Npcs/<NpcName>/` 下建立自己的剧情目录。


