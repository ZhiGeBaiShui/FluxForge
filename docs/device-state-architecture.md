# 装置状态与 tag 映射（最终架构）

> 状态：**当前实现**（2026-11-05，适配器单例化与 Create/Load 迁移后）。
> 本文描述最终边界与数据流；`docs/device-binding-design.md` 是 2026-11-01 的标签化绑定决策记录，
> 其状态同步部分已被本文取代。`docs/device-state-adapter-singleton-design.md` 记录本次迁移的
> 设计与决策，状态头写于实施前，当前行为以本文为准。
>
> 关键代码：
> `src/Logic/Devices/Interfaces/`、
> `src/Mod/DeviceState/`（`Contracts/` / `Catalog/` / `Adapters/` / `Sync/`）、
> `src/Mod/TagMapping/`、
> `src/Mod/DeviceServices/Core/DeviceHost.cs`。

## 1. 一句话

**Logic 拥有语义状态、行为与事件；Mod 拥有 tag 名、读写槽位与刷新。**
tag 是存档契约，但两类 tag 的读写路径不同：

- **流源 / 容器**使用原版字段级 tag（`CURRENT_PART_*`、`CONTAINER_TEMP_TAG`），
  由 `FlowStock` 整份读写 `FlowProperties`；
- **装置私有状态**由无状态 `DeviceStateAdapter` 单例按需 `Create` / `Load` / `Save`，
  由装置事件驱动。

## 2. 职责边界

| 层 | 拥有 | 不拥有 |
|---|---|---|
| **Logic**（`src/Logic`） | 语义状态（`IceBlockDevice.Water`、`CollectorDevice.Stock` 等）、行为（`TryReact` / `Route`）、事件（`IFlowNotifier`） | `GameItem`、`TagState`、tag 名、存档、Unity |
| **Mod / DeviceState**（`src/Mod/DeviceState`） | `DeviceStateAdapter` / `DeviceStateAdapter<TDevice>`、`DeviceDefinition`、`DeviceAdapterCatalog`（启动扫描 + 单例适配器）、每个装置的适配器、`FlowTagNaming`、`FlowPropertyTags`、`DeviceStateSync`（Changed 立即回写） | 生命周期、注册表、事件源 |
| **Mod / DeviceServices**（`src/Mod/DeviceServices`） | 装置实例的构造与登记、`DeviceHost`（`(Key, Adapter, Device)` 绑定）/ `DeviceRegistry` | tag 名与槽位语义 |
| **Mod / TagMapping**（`src/Mod/TagMapping`） | 流源 / 容器的 `FlowProperties` ↔ 原生字段 tag、`GameItemTags`（单 tag 读写） | 装置私有状态 |

依赖方向固定为 `Mod → Logic`；Logic 不引用任何 `FluxForge.Mod` 类型。

## 3. 两类 tag

### 3.1 流源 / 容器：字段级原生协议

| tag | 类型 / 单位 | 来源 |
|---|---|---|
| `CURRENT_PART_<ID>` | int，µL（0.001 mL） | 游戏 `InitLiquidContainerItem` 创建；`FlowStock` 读写 |
| `CONTAINER_TEMP_TAG` | double，摄氏度 | 流源 / 容器实际温度；`FlowStock` 读写 |
| `FLUXFORGE_TEMPERATURE_DELTA` | double，摄氏度 | 温度变化装置配置（冷阱 / 升温 / 降温共用）；正值升温，负值降温 |
| `FLUXFORGE_TEMPERATURE_MAX` / `_MIN` | double，摄氏度 | 升温 / 降温装置的温度上下限 |

tag 名只由 `FlowTagNaming.ForProperty(id, kind)` 生成；`FlowPropertyTags`
启动时按注册表生成一次只读缓存，热路径不再拼字符串：

```csharp
FlowTagNaming.ForProperty("water", FlowPropertyKind.Liquid)
    // CURRENT_PART_WATER
FlowTagNaming.ForProperty("container_temp", FlowPropertyKind.Scalar)
    // CONTAINER_TEMP_TAG
```

这是**原版协议**，不是本 mod 的通用持久化格式。`FlowStock` 遍历
`FlowPropertyRegistry.All` 生成名称并整份读写；发射器清空液体时也走同一条路径。
`FlowStock` 的底层单 tag 读写由 `GameItemTags` 完成。

`FlowStock` 的对外入口与独立提交入口：

- `Read(GameItem)` → 一份完整 `FlowProperties`；
- `Write(GameItem, FlowProperties)` → 写 tag，再 `Commit`（内部先 `SyncModifiedState`）；
- `Total(GameItem)` → 全部液体 tag 之和；
- `ItemTagCommit.Commit(GameItem)` → 先 `SyncModifiedState`，再刷新水质/贴图或标记贴图重取。

### 3.2 装置私有状态：无状态适配器

装置状态不是 `FlowProperties` 的一部分。每个装置类型有一个
`DeviceStateAdapter`；适配器在启动期实例化一次，之后作为单例复用：

```csharp
public abstract class DeviceStateAdapter
{
    public abstract object Create(GameItem item);           // tag -> 完整初始化的装置
    public abstract void Load(GameItem item, object device);  // tag -> 已有装置，不重建
    public abstract bool Save(GameItem item, object device);  // 装置 -> tag，返回是否写入
}
```

具体类型通过泛型基类声明装置类型，字段表负责默认的 Load / Save：

```csharp
public abstract class DeviceStateAdapter<TDevice> : DeviceStateAdapter
    where TDevice : class
{
    protected abstract TDevice CreateDevice(GameItem item);

    protected virtual IReadOnlyList<TagField<TDevice>> FieldMap { get; } // 默认空表

    protected virtual void LoadDevice(GameItem item, TDevice device);
    protected virtual bool SaveDevice(GameItem item, TDevice device);
    // Create / Load / Save 是 sealed override：只做一次向下转型
}
```

约束与实现细节：

- 适配器**不能有实例字段**（静态字段可以），必须有 public 无参构造；
  它不持有 `GameItem` / 装置 / `DeviceHost`。
- 有状态适配器用字段表声明映射：
  `TagFields.Int<IceBlockDevice>(tag, getter, setter)` /
  `TagFields.Double<IceBlockDevice>(tag, getter, setter)`。
  运行期 Load / Save 只调用委托，不做反射或动态代码生成。
- `TagFields.Int` 同时接受 int 成员和 double 成员；后者对应「int tag、double 成员」
  （冰块 `CURRENT_PART_WATER`），写入按 `(int)` 截断，与旧行为一致。
- `CollectorStateAdapter` 覆写 `LoadDevice` / `SaveDevice`：存量是整份
  `FlowProperties`，用 `ReplaceStock` 整体替换，Save 遍历 `FlowPropertyTags.All` 写回。
- `TemperatureConfigAdapter` 只 `CreateDevice` + `LoadDevice`，`SaveDevice` 恒为 false
  （温度来自 `items.json`，运行期不回写）。
- `FilterStateAdapter` / `TurnStateAdapter` / `LRightDownStateAdapter` /
  `TJunctionStateAdapter` / `DoubleEmitStateAdapter` 无持久化状态：只 `CreateDevice`
  新建装置，Load 为空操作、Save 返回 false。
  四个分流适配器分别服务具体装置类，不感知 `SplitDevice` 基类，也不存在共享的
  `SplitDevice` 适配器。

`DeviceAdapterCatalog` 启动时扫描本程序集里的具体适配器：

```text
DeviceStateAdapter<TDevice> 子类
  → 读取 [DeviceAdapter(key)]（AllowMultiple）
  → 实例化一个适配器单例
  → key → DeviceDefinition(Key, Adapter)
```

不检查实例字段、不反射成员；Scanner 只看到本程序集扫描和
`Activator.CreateInstance(Type)`，两者都不需要 capability。

- `Keys`：全部 key，按 ordinal 排序；
- `TryGet(key, out definition)`：未登记返回 false；
- `Get(key)`：未登记抛 `ArgumentException`；
- `FindFor(GameItem)`：按 key 顺序返回物品携带的全部定义（`item.IsTag(key)`）。

目录只负责定义与查找；**装置创建 / 回灌 / 回写全部由适配器和 `DeviceHost` 执行**。
没有 `DeviceAdapterCatalog.CreateDevice` / `CreateAdapter`，适配器也没有 `Device` 属性。

**帧**的含义：装置只持有语义状态；`Load` / `Save` 是显式的 tag 快照交换，
不是每帧轮询，也不是把 tag 缓存进装置。

### 3.3 `GameItemTags` 的读写语义

`GameItemTags` 是 Mod 层的静态单 tag 工具：

- 读 `modifiedState`：与 tooltip / 存档看到的值一致；缺失按 0；
- 写 `state`：`modifiedState` 是克隆，写它不会生效；
- `WriteInt` / `WriteDouble` 值没变返回 false；
- tag 不存在且值为 0 → 不创建 tag（避免给装置凭空造出 37 个零 tag）；
- tag 不存在且值非零 → `GetTag` + `Enable` 后写入；
- 不负责 `SyncModifiedState`、不触发刷新、不做生命周期兜底。

因此装置 `Save` 是稀疏的：只有真正有值且发生变化的 tag 会落盘。

## 4. 生命周期与数据流

### 4.1 绑定 / 读档

```text
ItemDeviceBinding.Bind(item)
  → 已有 Host
      → host.LoadAll()
      → 返回 true（幂等，不重建装置）
  → definitions = DeviceAdapterCatalog.FindFor(item)
  → definitions.Count == 0 → 返回 false（普通物品）
  → host = DeviceHost.Create(item, definitions)     // 唯一装置出生路径
      → 每个 definition：adapter.Create(item) → (Key, Adapter, Device) 绑定
  → DeviceRegistry.Register(host)
      → 建立 GameItem → Host、装置 → GameItem 反查
      → 订阅 IFlowNotifier.Changed / Exhausted
```

`Load` 只更新已有装置实例，绝不重新创建。绑定发生在「物品进入网格」(#2)
与「读档完成」(#6)；`ItemAttachment` 只负责把 `device` / anchor 等身份信息写成 tag。

### 4.2 装置变化上传：立即回写

`DeviceRegistry` 订阅每个装置的 `IFlowNotifier.Changed` / `IFlowNotifier.Exhausted`，
经 `DeviceHost` 转给 `DeviceStateSync`：

```text
装置修改语义状态
  ├─ IFlowNotifier.Changed（sender = 装置实例）
  │   → DeviceHost.OnDeviceChanged
  │   → DeviceStateSync.Save(host)（立即写 tag）
  └─ IFlowNotifier.Exhausted（sender = 装置实例）
      → DeviceHost.OnDeviceExhausted
      → DeviceStateSync.Exhausted(host)
      → host.SaveAll()
          → 有变化才 ItemTagCommit.Commit（内部先 SyncModifiedState）
      → DeviceRegistry.Unregister(item)
      → item.parentInventory.Expel(item)
```

要点：

- 事件是 `Action<IFlowNotifier>`，sender 是装置实例；Mod 用 sender 反查 Host；
- `Changed` 在事件栈里立即写 tag，不做帧末合并；
- `Exhausted` 是例外：必须**先同步回写**再注销 / 移出，否则物品离开后写不进 tag；
- 先播报 `Changed` 再播报 `Exhausted`（冰块化完的顺序），保证最终状态先落 tag；
- `host.SaveAll()` 遍历全部 `DeviceBinding` 调 `adapter.Save(item, device)`；
  只有 `Save` 真的写入时才需要提交与刷新；
- `ItemTagCommit.Commit` 是唯一提交入口，内部先 `SyncModifiedState`；
- `CollectorDevice` 实现 `IFlowNotifier`，但 `Exhausted` 是空访问器。

### 4.3 游戏侧直接改 tag

游戏原版会直接改物品 tag（倒水、药物溶解、环境降温等），不经过装置事件。
`DeviceStateReconcilePatch`（`GameItem.ValidateShapeState` postfix）在快照刷新后
只做一件事：`DeviceRegistry.GetHost(item)` 取到 Host 后立即 `host.LoadAll()`，
把 tag 的新值灌回已有装置。它**不写 tag、不做 finalize**。

这条规则沿用旧 `DeviceTagSyncPatch` 的时机：**挂在快照刷新之后**，而不是挂在
`ModifyTag` 上；否则 `modifiedState` 可能还是上一轮快照。

### 4.4 发射路径

发射器不是装置，不经过 `DeviceStateSync`：

```text
FlowRuntime.Emit(item)
  → FlowStock.Total(item) > 0
  → FlowStock.Read(item)         // 发射前重读，游戏可能直接改过 tag
  → Flows.Accept(new FlowEntity(...))
  → FlowProperties cleared = 液体归零、温度保留
  → FlowStock.Write(item, cleared)   // SyncModifiedState + 提交表现刷新
  → host != null ? host.LoadAll()    // 收集装置副本按新 tag 重灌
```

如果发射器同时挂有收集装置，发射清空 tag 后需要让收集装置的副本重新 `Load`，
否则新旧库存会叠加。实现为 `DeviceRegistry.GetHost(item)` 后调 `host.LoadAll()`，
不再通过 `FlowStock.ReadInto` 写进 `IFlowState`。

### 4.5 刷新语义

`ItemTagCommit.Commit(item)`：

- `LIQUID_CONTAINER_TAG` 物品：`WaterFeatureHelper.OnLiquidContainerPourOutOf`
  重算纯度 / 显示名 / 价格 / 扫描结果 / 暴露状态，再
  `SpriteHelper.UpdateWaterContainerSprite` 刷新贴图；
- 非容器物品：`SetSprite` 标记贴图需要重取。

`ItemTagCommit.Commit` 内部先执行 `SyncModifiedState`，再刷新外部表现；调用方不要再单独同步快照。

## 5. 已删除的旧路径

| 旧路径 | 现状 | 原因 |
|---|---|---|
| `IFlowState` | 删除 | 装置状态不再需要统一读写接口；适配器直接访问公开属性 |
| `IFlowNotifier` | 保留，去掉 `: IFlowState` | 事件仍携带 sender，但不再要求实现者暴露 `Keys/Get/Set` |
| `ITagStore` | 删除 | 单 tag 读写改由静态 `GameItemTags` 提供，不再包一层接口 |
| `GameItemTagStore` | 删除 | 同上；`GameItemTags` 直接读 `modifiedState`、写 `state` |
| `FakeTagStore` | 删除 | 适配器测试改用最小 `GameItem` / `TagSystem` / `TagState` 桩 |
| `DeviceAdapterCatalog.CreateDevice(key)` | 删除 | 装置创建改由适配器的 `Create(item)` 执行，目录只提供定义 |
| `DeviceAdapterCatalog.CreateAdapter(device)` / `DeviceStateAdapter.Device` | 删除 | 适配器改为启动期单例，不再按装置实例构造、不持有装置引用 |
| `DeviceStateAdapter.Load(ITagStore)` / `Save(ITagStore)` | 删除 | 改为 `Load(GameItem, device)` / `Save(GameItem, device)` |
| `DeviceStateSync.Load(host)` | 删除 | 回灌改由 `DeviceHost.LoadAll()` 承担 |
| `FlowTagMap` / `FlowTagMap.Binding` | 删除 | tag 名由 `FlowTagNaming.ForProperty` 唯一生成，不需要第二份映射表 |
| `FlowStock.ReadInto` | 删除 | 装置状态改走 `DeviceStateAdapter.Load` |
| `FlowStock.Write(GameItem, IFlowState)` | 删除 | 写回只服务 `FlowProperties`（流源 / 容器） |
| `DeviceTagSyncPatch` | 由 `DeviceStateReconcilePatch` 取代；postfix 只调 `host.LoadAll()` | 旧路径依赖 `IFlowState`，且一次遍历全部 37 槽；新 patch 只回灌，不写回 |
| `IceBlockDevice` / `TemperatureDevice` / `CollectorDevice` 的无参构造 | 删除 | 改由适配器 `CreateDevice` 传入初始状态；Filter / 四个分流变体仍保留无参构造 |

`FlowStock` 仍然保留，但职责收窄为「流源 / 容器 `FlowProperties` ↔ 原生字段 tag」。
`FlowProperties` 仍是纯数据对象，不再实现 `IFlowState`。

## 6. 新增一个有状态装置

1. Logic：实现装置类，公开语义状态；构造函数接收初始状态；需要存量变化或耗尽播报
   就实现 `IFlowNotifier`；不要实现 tag/`GameItem` 相关接口。
2. Mod：在 `src/Mod/DeviceState/Adapters/` 加一个 `DeviceStateAdapter<TDevice>` 子类，
   加上 `[DeviceAdapter("<key>")]`，实现 `CreateDevice`；用 `FieldMap`
   （`TagFields.Int/Double`）或覆写 `LoadDevice` / `SaveDevice` 搬字段。
3. Mod：不需要额外注册；`DeviceAdapterCatalog` 启动扫描会建立
   `key → DeviceDefinition`。重复 key 在扫描时抛错；未登记 key 的 `Get` 抛
   `ArgumentException`。
4. Mod：`DeviceRegistry` 订阅事件、`DeviceStateSync` 在 `Changed` 时立即 `SaveAll`；
   `ItemTagCommit.Commit`（内部先 `SyncModifiedState`）由 `DeviceStateSync` 统一调用。
5. `items.json`：声明 `device` key、anchor、附件。
6. 测试：`DeviceAdapterCatalog` 契约测试 + 适配器测试 + 挂载编排测试（最小 `GameItem` 桩，
   覆盖 Create / Load / Save、Bind / Flush / Exhausted 与稀疏写）；涉及 tag / 读档的改动按 README §9 做实机验收。

## 7. 不变量与非目标

- **不做防御式代码**：缺 tag 读 0；零值不创建 tag；生命周期由绑定 / 注销保证。
  不为 null、无效状态、不可能分支加 try/catch 或兜底。
- **不在每帧轮询 tag**：装置变化走事件 + `FlowDriver.Update` 末尾的 dirty flush；
  游戏侧变化走快照刷新后的 `DeviceStateReconcilePatch` → `LoadAll`。
- **不重建装置**：`DeviceHost.Create` 只创建一次；`Load` 只更新已有实例。
  `DeviceRegistry`、音频 / 动画反查都依赖稳定的装置引用。
- **适配器无状态**：单例不保存 `GameItem` / 装置 / Host；字段映射全部走静态字段表。
- **不缓存第二份属性清单**：属性、槽位全部从 `FlowPropertyRegistry` 派生；
  tag 名缓存 `FlowPropertyTags` 只是命名投影，仍由注册表与 `FlowTagNaming` 生成。
- **不删除原生液体 tag**：零值保留，避免破坏游戏容器语义。
- **不在 Logic 做数值钳制**：脏数据由边界层负责；当前 `FlowStock` /
  `GameItemTags` / 适配器只做转换。
- **不把装置状态塞进 `FlowProperties`**：流源 / 容器协议与装置私有状态是两套契约。
