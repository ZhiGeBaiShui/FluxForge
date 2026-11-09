# FlowEmitterMerchant

「装置推销员」NPC 的专属剧情目录。

```text
FlowEmitterMerchantNpc.cs        注册、来访间隔、生命周期接线
FlowEmitterMerchantPlan.cs       本 NPC 的显式剧情目录
FlowEmitterMerchantProgress.cs   到访与库存进度
FlowEmitterMerchantStock.cs      上货规则
Events/                          纯对话剧情
```

## Events

```text
Events/FlowEmitterArrival/         首次到访：见面、介绍货品、上架首批库存
Events/FlowEmitterSecondVisit/  第二次到访：介绍新货
Events/FlowEmitterGenericVisit/ 通用到访
Events/FlowEmitterFarewell/     卖完后的离场
```

剧情只负责对话、选项和上货，不再演示装置。

每个装载类自己声明：

```text
Phase      在哪个阶段执行
CanPlay()  当前是否满足前置条件
Complete() 结束时写入的标识
```

NPC 只维护显式的 `Stories` 目录并按阶段查找：

```text
FlowEmitterArrivalAssembly         -> Arrival
FlowEmitterSecondVisitAssembly  -> SecondVisit
FlowEmitterGenericVisitAssembly -> GenericVisit
FlowEmitterFarewellAssembly     -> SoldOut
```

## 新增剧情

```text
1. 写新的 StoryAssembly
2. 在 FlowEmitterMerchantPlan 的 Stories 列表加一行
```

剧情只依赖 `src/Story/Runtime`，不依赖 `src/Events`。

