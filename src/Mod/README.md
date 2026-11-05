# Mod 模块

## 要做什么

### 新增一个类：先判目录

按顺序问，第一个「是」就是归属：

| # | 它是 | 放 |
|---|---|---|
| 1 | 改变物品在网格中位置 / 归属的生命周期事件 | `GridBuilding/Lifecycle/` |
| 2 | 物品出生（定义 → 工厂 → 附件装配） | `ItemSpawn/` |
| 3 | 网格映射或每帧驱动 | `GridBuilding/` |
| 4 | tag 映射或 tag 读写工具 | `TagMapping/` |
| 5 | 装置状态适配器（装置属性 ↔ tag） | `DeviceState/` |
| 6 | 装置实例管理 | `DeviceServices/` |
| 7 | 配置模型或展示模型（items.json / sprites.json / tooltip） | `Content/` |
| 8 | 创建或更新 Unity UI 对象 | `Visual/` |
| 9 | 游戏 / 物品适配工具（坐标 / 方向 / tag 提交） | `Common/Game/`、`Common/Item/` |
| 10 | 对原版的功能接入 patch | 跟随所属功能目录 |
| 11 | mod 进程入口或每帧宿主 | `Bootstrap/` |
| 12 | 无状态、无依赖的工具类 | `Common/<场景>/` |

例外：存档边界与进程生命周期放 `Bootstrap/`；`EmporiumDestroyed` 是商城主场景销毁，归 `GridBuilding/Lifecycle/`。`GridCreatedPatch` 虽然带 `[HarmonyPatch]`，但属于网格生命周期，仍归 `GridBuilding/`；patch 不单独建顶层目录。

### 新增或修改一个装置（Mod 侧）

1. 先按 [`src/Logic/Devices/README.md`](../Logic/Devices/README.md) 写好 Logic 装置类。
2. 在 `src/Mod/DeviceState/Adapters/XxxStateAdapter.cs` 加适配器：

   ```csharp
   [DeviceAdapter("xxx")]
   public sealed class XxxStateAdapter : DeviceStateAdapter<XxxDevice>
   ```

   - `CreateDevice(GameItem)` 只负责按 tag 构造一台完整装置。
   - 需要持久化的成员用 `TagFields.Int/Double(tag, device => ..., (device, value) => ...)` 显式声明；不要用 `Expression.Compile`。
   - 有整份聚合状态时覆写 `LoadDevice` / `SaveDevice`（参考 `CollectorStateAdapter`）。
   - 适配器无实例字段，由 `DeviceAdapterCatalog` 启动扫描并复用单例；不在适配器里做登记、生命周期或 `DeviceRegistry` 调用。
3. 在 `assets/items.json` 声明 `"device": "xxx"`、`"anchorX"`、`"anchorY"` 和 `"attachments": ["device"]`；要进流量机网格再加 `"itemTypes": ["FLOW_DEVICE"]`。
4. 不要在物品创建或附件里手动 `new` 装置。统一走 `ItemDeviceBinding.Bind`：
   - 没有 Host：`DeviceHost.Create` → `adapter.Create`，只创建一次；
   - 已有 Host：立即 `LoadAll`，用 tag 更新已有实例。
5. 装置状态同步不要手写。Logic 装置只发 `IFlowNotifier.Changed` / `Exhausted`：

   ```text
   Changed   → host.SaveAll → 有变化才 ItemTagCommit.Commit
   Exhausted → SaveAll → DeviceRegistry.Unregister → Expel
   ```

   游戏侧直接改 tag 后的回灌由 `DeviceStateReconcilePatch`（`ValidateShapeState` 后立即 `LoadAll`）和 `FlowRuntime`（`FlowStock.Write` 后 `LoadAll`）负责。
6. 运行测试：

   ```powershell
   dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
   ```

   测试工程会编入 Logic、Presentation，以及 Mod 的 tag 工具、适配器和挂载编排。

### 新增或修改 tag 读写

- 单 tag 只用 `GameItemTags`：读 `modifiedState`、写 `state`，缺失按 0，等值或「不存在且写 0」不产生写入。没有 `ITagStore` / `GameItemTagStore` / `FakeTagStore`。
- 流源 / 容器整份属性用 `FlowStock.Read` / `Write` / `Total`；装置私有状态只走适配器。
- 写完 tag 交给 `ItemTagCommit.Commit`，不要自己拼 `SyncModifiedState` 和贴图刷新。
- tag 名由 `FlowTagNaming.ForProperty` 生成，不要手写。

### 新增生命周期钩子

一个事件一个切片（对称事件如 `#3b` / `#3c` 可共用一个文件），放 `GridBuilding/Lifecycle/`；完整事件表和反向索引见 [`GridBuilding/Lifecycle/README.md`](GridBuilding/Lifecycle/README.md)。批量拖拽只在 `StartGroupDrag`（`#3c`）注销，不要新增结束 / 取消 patch：结束走 `Expel` + `UncheckedAccept`，已有 `#3` / `#2` 会恢复登记。

### 扫描器约束

`manifest.xml` 的 `<Capabilities />` 保持为空；新增代码必须满足：

- 禁止 `Expression.Compile`。
- 禁止对计算出的 `Type` 做反射。
- 允许：本程序集 `Assembly.GetTypes()`、`Activator.CreateInstance(Type)`、字面量游戏类型上的 `AccessTools`，以及 Harmony 的 `___field` 注入（如 `___selectedItems`）。
- 对游戏代码打 Harmony patch 本身不受限；受限的是动态代码与计算类型反射。

## 常用 API

| API | 提供 |
|---|---|
| `DeviceAdapterCatalog.Keys` / `TryGet` / `Get` / `FindFor` | key → `DeviceDefinition`，以及按物品 tag 找装置定义 |
| `DeviceStateAdapter` / `DeviceStateAdapter<TDevice>` | `Create` / `Load` / `Save`；字段表与 `CreateDevice` / `LoadDevice` / `SaveDevice` |
| `TagFields.Int` / `Double` | 成员 ↔ tag 的显式 getter / setter 字段 |
| `DeviceHost.Create` / `LoadAll` / `SaveAll` / `Bindings` | 每件物品的 `DeviceBinding(Adapter, Device)` 列表与聚合读写；`Create` 是唯一出生入口 |
| `ItemDeviceBinding.Bind` | 确保装置存在；重复调用安全，已有实例不重建 |
| `DeviceRegistry.Register` / `Unregister` / `Clear` / `GetHost` / `GetRouter` / `GetReactor` / `GetItemForDevice` | 物品 ↔ Host、装置反查，并订阅 `IFlowNotifier` |
| `DeviceStateSync.Save` / `Exhausted` | Changed 立即回写、耗尽清理；通常由编排调用 |
| `GameItemTags.ReadInt` / `ReadDouble` / `WriteInt` / `WriteDouble` | 单 tag 读写；读快照、写活动状态 |
| `FlowTagNaming.ForProperty` / `FlowPropertyTags.All` | 属性 id + 种类 → 原版 tag 名；启动期只读缓存 |
| `FlowStock.Read` / `Write` / `Total` | 整份流源 / 容器属性 ↔ 原生液体 tag |
| `ItemTagCommit.Commit` | tag 写入后的统一提交与表现刷新 |
| `GridRegistry.Register` / `Get` / `Emit` / `Unregister` / `Clear` | 网格 ↔ `FlowRuntime` 注册与发射入口 |
| `FlowDriver.Update` / `LateUpdateVisuals` / `Reset` / `Shutdown` / `RetireByGrid` | 每帧驱动与场景 / 存档 / 网格边界清理 |

## 为什么 / 不做会怎样

- 依赖方向只能是 `src/Mod → src/Logic` 和 `src/Mod → src/Presentation`。Logic 或 Presentation 引用游戏 / Unity 类型后，纯 C# 测试无法编译。
- 手写 tag 同步会绕过 `ItemTagCommit.Commit`，`modifiedState`、tooltip 和存档可能停在旧值；给不存在的 tag 写 0 还会制造噪声。
- 在 `Load` 中重建装置，`DeviceRegistry` 的设备反查、音频和动画引用会全部失效；只允许更新已有实例。
- 适配器缺少 `[DeviceAdapter]`、key 重复，或 key 与 `items.json` 的 `device` 不一致时会抛异常；`DeviceAdapterCatalog.Get` 未命中也会抛 `ArgumentException`。key 同时是持久化 tag，改名等于换物品，旧存档状态丢失。
- 不给物品绑定就直接查 `DeviceRegistry`，拿不到 Router / Reactor；装置只由 `ItemDeviceBinding.Bind` 建立。
- 物品销毁没有 `ItemDestroyed` patch。`DeviceRegistry` 条目只在 `FlowDriver.Reset` / `Shutdown` 的 `Clear` 或装置 `Exhausted` 时清理，这是有界保留，不要当成即时销毁钩子。
- 批量拖拽补结束 / 取消 patch 会与 `Expel` + `UncheckedAccept` 触发的 `#3` / `#2` 重复处理。
- 使用 `Expression.Compile` 或对计算出的 `Type` 反射，会被 ModScanner 判为越权；`<Capabilities />` 为空时直接拒绝加载。
