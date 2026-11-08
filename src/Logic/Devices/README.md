# Logic 装置

## 要做什么

### 新增一个装置

1. **选接口与目录**
   - 改变流向 / 数量：`src/Logic/Devices/Routing/<类别>/`，实现 `IFlowRouter`。
   - 改变流源属性：`src/Logic/Devices/Reaction/<类别>/`，实现 `IFlowDevice`。
   - 有跨帧存量或会耗尽：再实现 `IFlowNotifier`。
   - 类用 `public sealed`。无状态装置提供 public 无参构造；有状态装置的构造函数接收初始状态，由适配器的 `CreateDevice` 调用。
2. **声明状态**
   - 装置自己持有 public 状态字段，例如 `IceBlockDevice.Water`、`CollectorDevice.Stock`。
   - 状态变化时 `Changed?.Invoke(this)`；耗尽时先 `Changed`，最后 `Exhausted?.Invoke(this)`。
   - 不声明 tag 名，不引用 `GameItem` / `TagState` / Unity。
3. **实现路由或反应**
   - `Route(...)` 返回 `true` 表示截停并接管；用传入的 `FlowSpawner` 的 `Accept` / `Kill` 处理分流、汇合或吸收。
   - `TryReact(...)` 返回 `true` 表示本帧发生反应并截停流源。
   - 分流变体继承 `SplitDevice`，只提供通道常量和发射数量；`Facing`（0..3）由 Mod 层按物品朝向写入。
4. **在 Mod 层登记**
   - 新建 `src/LogicIntegration/DeviceState/Adapters/XxxStateAdapter.cs`，声明 key 与装置类型：

     ```csharp
     [DeviceAdapter("xxx")]
     public sealed class XxxStateAdapter : DeviceStateAdapter<XxxDevice>
     ```

   - 实现 `CreateDevice(GameItem)`；需要持久化时再实现 `FieldMap`，用 `TagFields.Int/Double(tag, device => ..., (device, value) => ...)` 显式给 getter / setter。不要用 `Expression.Compile`。
   - 整份聚合状态（例如收集器存量）可以覆写 `LoadDevice` / `SaveDevice`；适配器保持无实例字段，由 `DeviceAdapterCatalog` 启动扫描并复用单例。
5. **声明物品数据**
   - 在 `assets/items.json` 写：

     ```json
     "device": "xxx",
     "anchorX": 0,
     "anchorY": 0,
     "attachments": ["device"]
     ```

   - 要放进流量机网格的物品，再加 `"itemTypes": ["FLOW_DEVICE"]`。
   - `device` 同时是持久化 tag 名；`anchorX` / `anchorY` 是基础形状的局部坐标，运行时由 `GridShape.ForwardTransform` 换算。
6. **测试**

   ```powershell
   dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
   ```

   装置类、适配器和 `DeviceAdapterCatalog` 的 key 都要有测试；读档、拖拽仍需进游戏验证。

### 使用已有装置

在 Mod 层只通过 `DeviceRegistry.GetRouter` / `GetReactor` 或 `DeviceHost.Router` / `Reactor` 取装置，不要自己 `new`，也不要在 `Load` 时重建。装置实例只由 `DeviceHost.Create` → `adapter.Create` 创建一次。

## 常用 API

| API | 提供 |
|---|---|
| `IFlowRouter.Route(FlowEntity, FlowSpawner, int, int)` | 路由流源并决定是否截停 |
| `IFlowRouter.Facing` | 装置朝向（0=上 1=右 2=下 3=左） |
| `IFlowDevice.TryReact(FlowEntity)` | 修改流源属性并决定是否截停 |
| `IFlowNotifier.Changed` / `Exhausted` | 状态变化与耗尽播报，签名是 `Action<IFlowNotifier>` |
| `SplitDevice(int[] relativeChannelDirs, int[] emitCounts = null)`（protected 基类） | 同步分流基类：通道、朝向与守恒分配 |
| `CollectorDevice(FlowProperties stock)`；`Stock` / `ReplaceStock` | 吸收并累积整份存量 |
| `IceBlockDevice(double water, double temperature)`；`Water` / `Temperature` | 冰块融化与耗尽 |
| `TemperatureDevice(double temperature)` | 每次穿过施加温度增量 |
| `FilterDevice()` | 过滤化学污染与重金属，不截停 |
| `FlowSpawner.Accept` / `Kill` / `KillAll` | 装置在 `Route` 中增删流源 |

## 为什么 / 不做会怎样

- 装置里引用 `GameItem` / `TagState` / Unity 后，Logic 单元测试无法编译，装置行为也不能脱离游戏验证。
- `[DeviceAdapter]` 缺失或 key 重复，`DeviceAdapterCatalog` 在启动扫描时抛异常；`DeviceAdapterCatalog.Get` 未命中抛 `ArgumentException`。`items.json` 的 `device` 必须与 attribute 的 key 完全一致。
- `device` key 是持久化 tag 名。改 key 等于换物品，旧存档会丢失装置身份；改名必须按读档兼容处理。
- 在 `Load` 里重建装置会让 `DeviceRegistry` 的设备反查、音频和动画引用全部失效；`Load` 只更新已有实例。
- 手写 tag 同步会绕过 `DeviceStateSync`；使用 `Expression.Compile` 还可能触发扫描器的权限拒绝。状态变化只发 `Changed`。
- 状态字段不公开，或适配器字段表漏项，`Load` 回灌不到、`Save` 也写不回；`DeviceStateAdapter<TDevice>` 只按声明的字段表搬运。
- 不实现 `IFlowNotifier`，状态变化不会回写 tag，tooltip 与存档会停在旧值。


