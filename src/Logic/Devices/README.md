# Logic 装置目录

纯 C# 装置层，不引用 `GameItem` / `TagState` / Unity。装置 key 与实例化
属于 Mod 层 `DeviceAdapterCatalog`；Logic 装置只暴露公开状态字段和
`IFlowNotifier` 事件，tag 映射由 Mod 层适配器负责。

```text
Devices/
├─ Interfaces/                 # 所有装置相关接口
│  ├─ IFlowDevice.cs           # 反应类：TryReact(FlowEntity)
│  ├─ IFlowRouter.cs           # 路由类：Route(FlowEntity, FlowSpawner, x, y)
│  └─ IFlowNotifier.cs         # Changed / Exhausted（Action<IFlowNotifier>）
├─ Routing/                    # 改变流源去向/数量的装置
│  ├─ Collector/
│  │  └─ CollectorDevice.cs
│  └─ Split/
│     ├─ Core/SplitDevice.cs   # 抽象基类：校验、Facing 换算、Route 算法
│     ├─ Turn/TurnDevice.cs
│     ├─ LRightDown/LRightDownDevice.cs
│     ├─ TJunction/TJunctionDevice.cs
│     └─ DoubleEmit/DoubleEmitDevice.cs
├─ Reaction/                   # 改变流源属性的装置
│  └─ IceBlock/
│     └─ IceBlockDevice.cs
```

实时反应规则（`FlowReactionRule` / `FlowReactionSolver`）和未接入的纯公式
在 `src/Logic/Reactions/`，见
[`Reactions/README.md`](../Reactions/README.md)；它们不是装置，仍由 Devices
之外的生命周期驱动。

游戏侧 `orientation → 方向码` 的换算是适配层职责，放在
`src/Mod/Common/Game/FlowFacingAdapter.cs`；Logic 只认识 `SplitDevice.Facing` 0..3，
没有 `IFacingAware` 之类的朝向接口。

## 新增装置：创建时声明什么

1. **目录和类**
   - 路由装置放 `Devices/Routing/<类别>/`，反应装置放 `Devices/Reaction/<类别>/`；
   - 实时反应规则继承 `FlowReactionRule`，放 `Reactions/Realtime/`，不属于装置；
   - 类必须是 `public sealed`（或 `SplitDevice` 的 `public sealed` 派生类），
     并提供 **public 无参构造**；Mod 层目录通过无参构造创建装置，测试也直接 `new`。

2. **角色接口**
   - 反应：`IFlowDevice`，实现 `TryReact(FlowEntity)`；装置从完整流源读取属性/速度；
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

3. **状态与事件**
   - 装置自己持有公开状态字段，例如 `IceBlockDevice.Water`、`CollectorDevice.Stock`；
   - 不再实现 `IFlowState`，不声明 `Keys` / `Get` / `Set`，也不认识 tag 名；
   - 状态变化或耗尽时实现 `IFlowNotifier`，事件保留 sender：
     ```csharp
     public event Action<IFlowNotifier> Changed;
     public event Action<IFlowNotifier> Exhausted;
     ```
   - 只有 `Changed` 的装置（如 Collector）用空访问器实现 `Exhausted`；
   - Mod 层 `DeviceStateSync` 负责订阅事件、标脏、帧末经适配器写 tag；
     Logic 不调用 `FlowStock`，不引用 `GameItem` / `TagState`。

4. **状态适配器**
   - 每个有持久化状态的装置，在 `src/Mod/DeviceState/` 有一个对应适配器；
   - 适配器负责 `Load(ITagStore)` / `Save(ITagStore)`，tag 名由 `FlowTagNaming` 统一生成；
   - 新增成员时只改装置公开字段和该适配器，不修改 Logic 的全局映射表。
5. **Mod 目录登记**
   - 在 `src/Mod/DeviceState/Adapters/` 新增适配器，并用 attribute 声明 device key：
     ```csharp
     [DeviceAdapter("xxx", typeof(XxxDevice))]
     public sealed class XxxStateAdapter : DeviceStateAdapter<XxxDevice>
     ```
   - `DeviceAdapterCatalog` 启动扫描时建立：
     ```text
     key → 装置类型
     装置类型 → 适配器
     ```
   - key 必须唯一，并与 `items.json` 的 `device` 完全一致。
     未登记的 key 在 `CreateDevice` 的字典索引处直接抛错；重复 key 在启动扫描时抛错。

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
     （`ItemDeviceBinding.Bind`）和读档完成（生命周期 #6），两处都由 Mod 适配器执行 `Load`；
   - 不需要在装置里保存 `GameItem` / `TagState`，也不需要引用 Unity。

> 注意：`device` key 同时是持久化 tag 名。改 key 等于换物品，旧存档会丢失装置身份；
> 改名必须按读档兼容处理，不能只改 `items.json`。

详细设计：`docs/device-binding-design.md`（历史设计稿；当前绑定已改为 tag 驱动，差异见该文状态头）。
