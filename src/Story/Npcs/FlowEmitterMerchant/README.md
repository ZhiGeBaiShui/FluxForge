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
Events/FlowEmitterArrival/      首次到访：邀请、赠送入门材料与便条、验收95%酸试剂
Events/FlowEmitterSecondVisit/  第二次到访：正式售卖全新装置
Events/FlowEmitterGenericVisit/ 通用到访
```

剧情只负责对话、选项和上货，不再演示装置。

## 上货

```text
首次到访：不售货；接受后赠送90%酸试剂、2发射器、1升温、1净化及便条。
验收：桌面酸试剂浓度至少95%；缺失或不足回到等待选项。
成功：赞扬；放弃：鼓励。两条路线均在告别时赠送4转向、1加速、1降温（破旧）。
首次剧情结束时记录完成标识并离店，七天后再访。
选择“这次先不了”时只离店，不写首次剧情完成标识；下次开场缩短为
“你好，这次你有兴趣尝试吗？”，仍提供接受/拒绝选项。
成功和主动放弃在告别结束时写入首次剧情完成标识。
后续到访：Refresh
```

第二次到访使用独立的 `PlaceSecondVisit` 库存：冷萃1、净化1、发射器2、转向4、
升温1、降温1、变速2，全部为正常售卖物品，不附加破旧效果。

`Refresh` 依赖原版客户离店时的非拥有物品清理，再按当前剧情进度重新上货。实际入桌统一调用官方：

```csharp
PlayerStore.Instance.AddDirectSellingItemToTable(item)
```

不再给库存打自定义归属 tag。

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
```

## 新增剧情

```text
1. 写新的 StoryAssembly
2. 在 FlowEmitterMerchantPlan 的 Stories 列表加一行
```

剧情只依赖 `src/Story/Runtime`，不依赖 `src/Events`。

