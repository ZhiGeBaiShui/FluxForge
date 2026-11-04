# Logic 装置目录

纯 C# 装置层，不引用 `GameItem` / `TagState` / Unity。Mod 层通过
`FlowDeviceCatalog` 的 key 创建装置；状态通过 `IFlowState` + `IFlowNotifier`
映射到物品 tag。

```text
Devices/
├─ Interfaces/                 # 所有装置相关接口
│  ├─ IFlowDevice.cs           # 反应类：TryReact(FlowProperties)
│  ├─ IFlowRouter.cs           # 路由类：Route(FlowEntity, FlowSpawner, x, y)
│  ├─ IFlowNotifier.cs         # 播报；继承 IFlowState
│  └─ IFlowState.cs            # Keys + Get/Set 属性槽
├─ Routing/                    # 改变流源去向/数量的装置
│  ├─ Collector/
│  │  └─ CollectorDevice.cs
│  └─ Split/
│     ├─ SplitDevice.cs        # 抽象基类：校验、Facing 换算、Route 算法
│     ├─ Turn/TurnDevice.cs
│     ├─ LRightDown/LRightDownDevice.cs
│     ├─ TJunction/TJunctionDevice.cs
│     └─ DoubleEmit/DoubleEmitDevice.cs
├─ Reaction/                   # 改变流源属性的装置
│  └─ IceBlock/
│     └─ IceBlockDevice.cs
├─ FlowDeviceCatalog.cs        # items.json device key → 无参工厂（唯一登记点）
```

实时反应规则（`FlowReactionRule` / `FlowReactionSolver`）和未接入的纯公式
在 `src/Logic/Reactions/`，见
[`Reactions/README.md`](../Reactions/README.md)；它们不是装置，仍由 Devices
之外的生命周期驱动。

游戏侧 `orientation → 方向码` 的换算是适配层职责，放在
`src/Mod/Game/FlowFacing.cs`；Logic 只认识 `SplitDevice.Facing` 0..3，
没有 `IFacingAware` 之类的朝向接口。

## 新增装置：创建时声明什么

1. **目录和类**
   - 路由装置放 `Devices/Routing/<类别>/`，反应装置放 `Devices/Reaction/<类别>/`；
   - 实时反应规则继承 `FlowReactionRule`，放 `Reactions/Realtime/`，不属于装置；
   - 类必须是 `public sealed`（或 `SplitDevice` 的 `public sealed` 派生类），
     并提供 **public 无参构造**；`FlowDeviceCatalog` 和测试都直接 `new`。

2. **角色接口**
   - 反应：`IFlowDevice`，实现 `TryReact(FlowProperties)`；
   - 路由：`IFlowRouter`，实现 `Route(FlowEntity, FlowSpawner, cellX, cellY)`；
   - 发射不是装置：流源由 `FlowRuntime.Emit` 直接从物品存量生成，
     Logic 层没有「发射装置类」；
   - 分流变体：继承 `SplitDevice`，只传通道常量和发射数量，不重写 `Route`：
     ```csharp
     public sealed class XxxSplitDevice : SplitDevice
     {
         public XxxSplitDevice()
             : base(new[] { 3, 2 }, new[] { 1, 1 })
         {
         }
     }
     ```
   - 多格装置的中心格/生成格不属于 Logic：`FlowGridAdapter` 在网格注册时读取物品
     tag，用 `GridShape.ForwardTransform` 算出当前世界格，并且**只在该中心格登记
     这件物品**。流源沿形状的其它格走到中心格时，`FlowManager` 才用中心格调用
     `Route`。`FlowManager` / `IFlowGrid` / `SplitDevice` 都不需要知道中心格，
     也不出现 `FlowCell` / `Anchor` 等类型。

3. **状态接口（有属性要落 tag 时）**
   - 实现 `IFlowState`：
     ```csharp
     public IReadOnlyList<FlowPropertyKey> Keys { get; }
     double Get(FlowPropertyKey key);
     void Set(FlowPropertyKey key, double value);
     ```
   - `Keys` 只声明这个装置**真正拥有**的属性，不要声明全部 37 槽：
     ```csharp
     // 只关心水量 + 温度
     private static readonly FlowPropertyKey[] OwnedKeys =
     {
         FlowProps.Water,
         FlowProps.Temperature,
     };

     public IReadOnlyList<FlowPropertyKey> Keys => OwnedKeys;
     ```
   - `Get` 对未拥有的 key 可以返回 0，`Set` 可以忽略；因为 `FlowStock`
     只遍历 `Keys`，未声明的槽位不会参与 tag 读写。
   - `FlowProperties`、`CollectorDevice` 是完整状态，`Keys`
     返回 `FlowPropertyRegistry.AllKeys`；`IceBlockDevice` 只返回
     `Water` / `Temperature`。

4. **事件接口（状态变化要写回 tag 时）**
   - 实现 `IFlowNotifier`（它继承 `IFlowState`）：
     ```csharp
     public event Action<IFlowNotifier> Changed;
     public event Action<IFlowNotifier> Exhausted;
     ```
   - 状态变化后触发 `Changed`；`DeviceRegistry.Register` 会自动订阅，
     `FlowDeviceEvents.OnChanged` 调 `FlowStock.Write(item, device)`，
     只写 `Keys` 里的槽位。
   - 装置会从网格销毁（如冰块化完）时触发 `Exhausted`；
     永远不会销毁的装置（Collector）用空访问器实现，保持订阅链路统一。
   - 不要在 Attach 里读 tag，也不要自己写 `FlowStock`；存量在物品挂上容器时
     由 `ItemDeviceBinding.Bind` 调 `FlowStock.ReadInto` 灌入，之后由 `Changed` 写回。

5. **目录登记**
   - 在 `FlowDeviceCatalog` 的静态字典里加一行：
     ```csharp
     ["xxx"] = () => new XxxDevice(),
     ```
   - key 必须唯一，并与 `items.json` 的 `device` 完全一致。
     **当前没有 key 的启动校验**：绑定端是「遍历目录里的全部 key，命中哪个 tag 就建哪个装置」，
     所以 `items.json` 写了未知 key 时只会留下一个没有绑定方的 tag，**不会抛异常**；
     拼写错误必须靠评审或后续加启动校验发现。

6. **items.json**
   - 声明 `device`（同时是持久化 tag 名）、中心格和 `device` 附件：
     ```json
     "device": "xxx",
     "anchorX": 0,
     "anchorY": 0,
     "attachments": ["device"],
     ```
   - `anchorX` / `anchorY` 是基础形状的局部坐标，运行时由
     `GridShape.ForwardTransform` 换算；`device` 附件会把它写成
     `FLUXFORGE_DEVICE_ANCHOR_X/Y` tag，缺字段直接抛异常。
   - 需要放进流量机网格的物品，再加：
     ```json
     "itemTypes": ["FLOW_DEVICE"],
     ```
   - 需要贴图、名称、tooltip 的，按现有物品的字段补齐；`flow_container`
     只给容器类物品用。

7. **不需要声明的东西**
   - 不需要为装置单独写 Mod attachment：所有装置统一走 `DeviceAttachment`；
   - 不需要实现朝向接口：分流器的 `Facing` 由 `FlowGridAdapter.RegisterCore` 直接写 `SplitDevice.Facing`；
   - 不需要在 Attach 时读 tag：tag 是存档真相，绑定发生在物品挂上网格
     （`ItemDeviceBinding.Bind`）和读档完成（生命周期 #6），两处都会 `FlowStock.ReadInto`；
   - 不需要在装置里保存 `GameItem` / `TagState`，也不需要引用 Unity。

> 注意：`device` key 同时是持久化 tag 名。改 key 等于换物品，旧存档会丢失装置身份；
> 改名必须按读档兼容处理，不能只改 `items.json`。

详细设计：`docs/device-binding-design.md`（历史设计稿；当前绑定已改为 tag 驱动，差异见该文状态头）。
