# 蒸馏器电池附件与资源映射

## 配置

物品定义使用 `"device": "distiller"`，attachments 加入 `"battery"` 和 `"device"`。
`battery` 只创建一个原版电池槽和电源面板，不添加燃料槽、模块槽或其它资源入口。
不要同时附加另一个覆盖 contentWindow 的附件。

配置 tag `FLUXFORGE_DISTILLER_WATER_PER_ENERGY`（double）表示每单位原版电池电量
允许蒸馏多少 µL 水。没有配置时为 0，不蒸馏、不耗电，等待正式平衡值。

本次仅准备附件、资源逻辑和绑定，不自动注册成品物品、分配贴图或接入左右端口。

## 自定义映射

现有 DeviceStateAdapter 的 LoadDevice/SaveDevice 就是自定义映射扩展点，
FieldMap 只是 tag 字段的一种便捷实现，不需要把电池转换成机器的虚拟电量 tag。

DistillerStateAdapter 在 LoadDevice 中读取参数，再调用 BatteryEnergyMapping.Load：
通过 MachineHelper.GetBatterySlot(machine).childItem 每次现查电池，回灌 DistillerDevice.Energy。
适配器及映射单例都不缓存 GameItem、槽位或电池引用。

DistillerDevice.Distill 调用纯蒸馏公式，直接扣减 Energy，并触发 Changed。
DeviceHost 的同步保存链调用 BatteryEnergyMapping.Save，将剩余电量直接写回实际电池，
提交电池状态并刷新电池贴图。不维护待扣费累计或确认协议。
电量是原版整数单位，不足一个单位的有效抽水消耗按一个单位向上取整。

接入流源时，先 host.LoadAll() 读取资源，再在同一调用栈内加工并同步写回。
机器接收/加工/储水/输出仍由机器宿主负责。
本次不实现加工队列或延迟输出状态机。

## 读档与运行时刷新

- 原版 contentWindow 保存槽位中的实际电池及其 tag，不重复保存逻辑层 Energy。
- 首次创建 adapter 时可能尚未恢复电池：空槽读 0，不写回、不生成空电池。
- 槽位插入/移出结束回调与物品 onLoaded 回灌已有 Host。
- OnGameLoadedLate 在原有 Bind/Rebuild 后调用 DeviceRegistry.ReloadAll，
  确保所有已绑定机器（包括不在网格中的机器）读取最终恢复的子物品。
- 电池 ValidateShapeState 时沿父关系刷新所属机器，支持原版充电和放电。
- Load 只读，读档恢复不会调用消费逻辑，因此恢复过程本身不会扣电。

原生 UI 的打开、子物品恢复顺序及充电表现仍需游戏内验证。
