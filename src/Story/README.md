# Story Framework

`src/Story` 分为三层：

```text
VisitRule      决定 NPC 今天来不来、以什么身份来
StoryEvent     决定这次触发哪段剧情
StoryContent   只负责对白、选项和同步动作
```

新增剧情只需要新增 `StoryEvent` 注册和内容，不修改 NPC 控制器。

## 1. 目录

```text
src/Story/
├─ Runtime/
│  ├─ Model/          选择与执行共用的数据模型
│  ├─ Selection/      到访规则 / 剧情事件选择器（纯逻辑）
│  ├─ State/          modData 状态适配
│  ├─ Direction/      通用 NPC 控制器、访客会话
│  ├─ Bootstrap/      剧情框架组合根
│  ├─ Graph/          StoryNode / StoryEdge
│  ├─ Execution/      StorySequence
│  ├─ Dialogue/       原版 Dialogue 装配与逐行停顿
│  ├─ Contracts/      StoryAssembly
│  └─ StoryPlayer.cs  单段剧情执行器
└─ Npcs/
   ├─ FlowEmitterMerchant/
   │  ├─ FlowEmitterMerchantStory.cs   身份 + 到访规则 + 剧情事件
   │  ├─ FlowEmitterMerchantStock.cs   奥雅专属库存
   │  ├─ FlowEmitterMerchantProgress.cs
   │  └─ Events/                       纯剧情内容
   └─ AcidBuyer/
      ├─ AcidBuyerStory.cs             身份 + 到访规则 + 剧情事件
      ├─ AcidBuyerStock.cs
      ├─ AcidBuyerTrade.cs
      ├─ AcidBuyerProgress.cs
      └─ Events/                       纯剧情内容
```

## 2. 到访规则

```csharp
VisitRule
{
    Id
    NpcId
    Priority
    Chance       // 0..1
    CooldownDays
    Kind         // Dialogue / Sell / Buy / SellBuy
    Condition
    OnVisitStarted
}
```

选择语义：

```text
条件成立 + 冷却结束
→ Priority 最高
→ 只判定一次 Chance
→ 没抽中当天不来，不回退低优先级规则
```

`OnVisitStarted` 用于兼容旧进度计数；新 NPC 不需要设置，框架会自动增加通用访问次数。

## 3. 剧情事件

```csharp
StoryEvent
{
    Id
    NpcId
    Trigger      // VisitStarted / TradeCompleted / VisitEnding
    Priority
    MaxTimes     // 0 = 无限
    ExclusionGroup
    Condition
}
```

同一 NPC、同一 Trigger：

```text
条件成立
→ 未超过 MaxTimes
→ ExclusionGroup 未被消费
→ Priority 最高
→ 只执行一个
```

`VisitStarted`、`TradeCompleted`、`VisitEnding` 都由通用运行器处理。

交易内容完成时调用：

```csharp
StoryNpcRuntime.QueueTradeCompleted(
    new StoryTradeResult(success, amount, itemKey));
```

运行器会在当前剧情结束后选择 `TradeCompleted` 事件，再进入 `VisitEnding`。

## 强制离场

每个访客都设置：

```csharp
OnDismiss = () => StoryNpcRuntime.CancelForClient(identifier)
```

客户被强制遣散/离场时：

```text
OnDismiss
→ StoryNpcRuntime.CancelForClient
→ StoryPlayer.Cancel
→ 中断原版对话
→ 停止等待中的 StorySequence
→ 不再执行 Complete / Stock / Outcome
```

这样 NPC 已经离场时，不会继续留下对白、选项或上货逻辑。

## 4. StoryContext

剧情条件只能读稳定上下文：

```csharp
ctx.VisitCount
ctx.VisitKind
ctx.Trigger
ctx.TradeResult
ctx.HasFlag(key)
ctx.HasTag(tag)
```

不允许条件直接引用某个 NPC 类的字段。

## 5. 内容

`StoryAssembly` 只保留：

```text
Entry
Complete()
```

不再有 `Phase` / `CanPlay()`。是否该播放由 `StoryEvent` 决定。

`StoryPlayer` 继续负责：

```text
边执行 → 节点选项 → 边执行 → 退出
```

`StoryEdge.Branch` 只用于单段剧情内部的分支，不用于选择剧情事件。

## 6. 新增剧情

```text
1. 写 StoryAssembly 内容
2. 在对应 NPC 的 Story 文件中注册 StoryEvent
```

不需要修改：

```text
NpcStoryHost
StoryPlayer
StoryEdge
NPC 到访控制器
```

新增到访规则同理，只改对应 NPC 的 Story 文件。

## 7. 边界

```text
剧情不引用 src/Events
不锁网格
不接管原版 Dialogue UI
不实现 Yarn/Ink 解释器
```

原版 `Dialogue`、`DialogUIManager`、`StoreClient` 继续负责表现和交易。
