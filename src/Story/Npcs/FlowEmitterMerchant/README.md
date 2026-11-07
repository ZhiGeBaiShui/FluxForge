# FlowEmitterMerchant

这是「装置推销员」这个 NPC 的专属事件层。

本目录只放这个 NPC 自己的内容：

```text
FlowEmitterMerchantNpc.cs        注册、来访间隔、生命周期接线
FlowEmitterMerchantPlan.cs       本 NPC 的显式剧情目录
FlowEmitterMerchantProgress.cs   这个 NPC 的剧情进度标识
FlowEmitterMerchantStock.cs      这个 NPC 的上货规则
Events/                          这个 NPC 对应的剧情
```

## Events

```text
Events/FlowEmitterDemo/       剧情 1：首次到访与装置演示
Events/FlowEmitterFarewell/   剧情 2：库存卖完后的离场赠礼
```

每个剧情装载类自己声明：

```text
Phase      在哪个阶段执行
CanPlay()  当前是否满足前置条件
Complete() 结束时需要写入的标识或动作
```

首访相关标识有两个：

```text
ArrivalSeenKey  到访剧情是否已经看完（DemoAssembly.Complete 写入）
FirstStoryKey   首次整条线是否完成（FarewellAssembly.Complete 写入）
```

`ArrivalSeenKey` 防止库存没卖完时下次到访重播到访剧情；`FirstStoryKey` 表示首次剧情与离场赠礼都已完成。

NPC 只维护显式的 `Stories` 目录，并按阶段查找：

```text
FlowEmitterDemoAssembly     -> Arrival
FlowEmitterFarewellAssembly -> SoldOut
```

新增剧情时：

```text
1. 写新的 StoryAssembly
2. 在 NPC 的 Stories 列表加一行
```

NPC 不写具体剧情判断，剧情装载类也不反向依赖 NPC 的到店流程。

剧情只依赖共享的 `src/Story/Runtime`，不反向依赖 NPC。

## 边界

```text
这个目录 = 一个 NPC 的事件层
src/Story/Runtime = 通用剧情运行时
```

以后新增 NPC，应在 `src/Story/Npcs/<NpcName>/` 下建立自己的事件层，而不是把它的剧情放进其他 NPC 的目录。
