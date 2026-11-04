# 装置状态与 tag 映射（最终架构）

> 状态：**当前实现**（2026-11-05，装置状态适配器改造后）。
> 本文描述最终边界与数据流；`docs/device-binding-design.md` 是 2026-11-01 的标签化绑定决策记录，
> 其状态同步部分已被本文取代。
>
> 关键代码：
> `src/Logic/Devices/Interfaces/`、
> `src/Mod/DeviceState/`（含 `DeviceStateSync.cs` / `DeviceAdapterCatalog.cs`）、
> `src/Mod/TagMapping/`、
> `src/Mod/DeviceState/Sync/DeviceStateReconcilePatch.cs`。

## 1. 一句话

**Logic 拥有语义状态、行为与事件；Mod 拥有 tag 名、读写槽位与刷新。**
tag 是存档契约，但两类 tag 的读写路径不同：

- **流源 / 容器**使用原版字段级 tag（`CURRENT_PART_*`、`CONTAINER_TEMP_TAG`），
  由 `FlowStock` 整份读写 `FlowProperties`；
- **装置私有状态**是 Mod 适配器的一帧，由 `DeviceStateAdapter` 按需 `Load` / `Save`，
  由装置事件驱动。

## 2. 职责边界

| 层 | 拥有 | 不拥有 |
|---|---|---|
| **Logic**（`src/Logic`） | 语义状态（`IceBlockDevice.Water`、`CollectorDevice.Stock` 等）、行为（`TryReact` / `Route`）、事件（`IFlowNotifier`） | `GameItem`、`TagState`、tag 名、存档、Unity |
| **Mod / DeviceState**（`src/Mod/DeviceState`） | `ITagStore`、`DeviceStateAdapter`、每个装置的适配器、`FlowTagNaming`、`FlowPropertyTags`、`DeviceStateSync`（脏标记 + 帧末 flush） | 生命周期、注册表、事件源 |
| **Mod / DeviceServices**（`src/Mod/DeviceServices`） | 装置实例的构造与登记、`DeviceHost` / `DeviceRegistry` | tag 名与槽位语义 |
| **Mod / TagMapping**（`src/Mod/TagMapping`） | 流源 / 容器的 `FlowProperties` ↔ 原生字段 tag、`GameItemTagStore` | 装置私有状态 |

依赖方向固定为 `Mod → Logic`；Logic 不引用任何 `FluxForge.Mod` 类型。

## 3. 两类 tag

### 3.1 流源 / 容器：字段级原生协议

| tag | 类型 / 单位 | 来源 |
|---|---|---|
| `CURRENT_PART_<ID>` | int，µL（0.001 mL） | 游戏 `InitLiquidContainerItem` 创建；`FlowStock` 读写 |
| `CONTAINER_TEMP_TAG` | double，摄氏度 | 容器 / 装置配置；`FlowStock` 读写 |

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

`FlowStock` 的对外入口与独立提交入口：

- `Read(GameItem)` → 一份完整 `FlowProperties`；
- `Write(GameItem, FlowProperties)` → 写 tag，再 `Commit`（内部先 `SyncModifiedState`）；
- `Total(GameItem)` → 全部液体 tag 之和；
- `ItemTagCommit.Commit(GameItem)`（internal）→ 先 `SyncModifiedState`，再刷新水质/贴图或标记贴图重取。

### 3.2 装置私有状态：适配器 / 帧

装置状态不是 `FlowProperties` 的一部分。每个有状态装置有一个
`DeviceStateAdapter`：

```csharp
public abstract class DeviceStateAdapter
{
    public abstract object Device { get; }
    public abstract void Load(ITagStore tags);   // tag -> 装置公开属性，缺 tag 读 0
    public abstract bool Save(ITagStore tags);   // 装置公开属性 -> tag，返回是否有值变化
}

public abstract class DeviceStateAdapter<TDevice> : DeviceStateAdapter
    where TDevice : class
{
    // 子类构造时传入具体装置
}
```

`DeviceAdapterCatalog` 启动时扫描程序集里所有具体适配器，读取
`[DeviceAdapter]` 建立 `key → 装置类型`，按 `DeviceStateAdapter<TDevice>`
泛型参数建立 `装置类型 → 适配器`；新增适配器不需要改 switch。

适配器只搬自己那几个字段，例如：

- `IceBlockStateAdapter`：`CURRENT_PART_WATER`（int）↔ `Water`，
  `CONTAINER_TEMP_TAG`（double）↔ `Temperature`；
- `CollectorStateAdapter`：按注册表读写全部液体 + 温度；
- `TemperatureConfigAdapter`：只 `Load` `CONTAINER_TEMP_TAG`，`Save` 恒为 false
  （温度来自 `items.json`，运行期不回写）；
- `TurnStateAdapter` / `LRightDownStateAdapter` / `TJunctionStateAdapter` / `DoubleEmitStateAdapter` / `FilterStateAdapter`：无持久化状态，`Load` 空、`Save` false。
  四个分流适配器分别服务具体装置类，不感知 `SplitDevice` 基类。

**帧**的含义：装置只持有语义状态；`Load` / `Save` 是显式的 tag 快照交换，
不是每帧轮询，也不是把 tag 缓存进装置。

### 3.3 `GameItemTagStore` 的读写语义

`GameItemTagStore` 是 `ITagStore` 的物品实现：

- 读 `modifiedState`：与 tooltip / 存档看到的值一致；
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
  → 已有 Host：DeviceStateSync.Load(host) 后返回（幂等，不重建）
  → 按 item 上的 device tag 从 DeviceAdapterCatalog 构造装置
  → DeviceAdapterCatalog.CreateAdapter(device) 选适配器
  → adapter.Load(new GameItemTagStore(item))
  → DeviceRegistry.Register(item, devices, adapters)
  → DeviceRegistry 订阅 IFlowNotifier.Changed / Exhausted
```

绑定发生在「物品进入网格」(#2) 与「读档完成」(#6)，不要求在 `ItemAttachment`
里读 tag。`ItemAttachment` 只负责把 `device` / anchor 等身份信息写成 tag。

### 4.2 装置变化上传：脏标记 + 帧末 flush

`DeviceStateSync` 由 `DeviceRegistry` 统一订阅每个装置的
`IFlowNotifier.Changed` / `IFlowNotifier.Exhausted`，本身不做写 tag：

```text
装置修改语义状态
  ├─ IFlowNotifier.Changed（sender = 装置实例）
  │   → DeviceHost.OnDeviceChanged
  │   → DeviceStateSync.MarkDirty(host)（不写 tag）
  └─ IFlowNotifier.Exhausted（sender = 装置实例）
      → DeviceHost.OnDeviceExhausted
      → DeviceStateSync.Exhausted(host)
      → 先同步 Save(host)：adapter.Save(new GameItemTagStore(item))
          → 有变化才 ItemTagCommit.Commit（内部先 SyncModifiedState）
      → DeviceRegistry.Unregister(item)
      → item.parentInventory.Expel(item)

每次 FlowDriver.Update 末尾（一帧一次，遍历完全部网格之后）
  → DeviceStateSync.Flush()
      → 遍历 dirty 的 DeviceHost
      → adapter.Save(new GameItemTagStore(item))
      → 有变化才 ItemTagCommit.Commit(item)（内部先 SyncModifiedState）
```

要点：

- 事件是 `Action<IFlowNotifier>`，sender 是装置实例；Mod 用 sender 反查 Host；
- 普通的 `Changed` 只标脏，不在事件栈里写 tag；一帧内的多次变化合并成一次写入；
- `Exhausted` 是例外：必须**先同步 flush** 再注销 / 移出，否则物品离开后写不进 tag；
- 先播报 `Changed` 再播报 `Exhausted`（冰块化完的顺序），保证最终状态先落 tag；
- 只有 `Save` 真的写入时才需要刷新；`Save` 返回 false 说明 tag 已经是目标值；
- `ItemTagCommit.Commit` 是唯一提交入口（internal），内部先 `SyncModifiedState`，由 `DeviceStateSync` 调用；
- `CollectorDevice` 实现 `IFlowNotifier`，但 `Exhausted` 是空访问器。

### 4.3 游戏侧直接改 tag

游戏原版会直接改物品 tag（倒水、药物溶解、环境降温等），不经过装置事件。
`DeviceStateReconcilePatch`（`GameItem.ValidateShapeState` postfix）在快照刷新后
只做一件事：`DeviceRegistry.GetHost(item)` 取到 Host 后调 `DeviceStateSync.Load(host)`，
对全部适配器执行 `Load(new GameItemTagStore(item))`，把 tag 的新值灌回装置。
它**不写 tag、不做 finalize**，避免在快照刷新路径里产生第二次写与刷新。

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
  → FlowStock.Write(item, cleared)   // SyncModifiedState + Refresh
```

如果发射器同时挂有收集装置，发射清空 tag 后需要让收集装置的副本重新 `Load`，
否则新旧库存会叠加。实现为 `DeviceRegistry.GetHost(item)` 后调
`DeviceStateSync.Load(host)`，不再通过 `FlowStock.ReadInto` 写进 `IFlowState`。

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
| `FlowTagMap` / `FlowTagMap.Binding` | 删除 | tag 名由 `FlowTagNaming.ForProperty` 唯一生成，不需要第二份映射表 |
| `FlowStock.ReadInto` | 删除 | 装置状态改走 `DeviceStateAdapter.Load` |
| `FlowStock.Write(GameItem, IFlowState)` | 删除 | 写回只服务 `FlowProperties`（流源 / 容器） |
| `DeviceTagSyncPatch` | 由 `DeviceStateReconcilePatch` 取代；postfix 只调适配器 `Load` | 旧路径依赖 `IFlowState`，且一次遍历全部 37 槽；新 patch 只回灌，不写回 |

`FlowStock` 仍然保留，但职责收窄为「流源 / 容器 `FlowProperties` ↔ 原生字段 tag」。
`FlowProperties` 仍是纯数据对象，不再实现 `IFlowState`。

## 6. 新增一个有状态装置

1. Logic：实现装置类，公开语义状态；需要存量变化或耗尽播报就实现 `IFlowNotifier`；
   不要实现 tag/`GameItem` 相关接口。
2. Mod：在 `src/Mod/DeviceState/Adapters/` 加一个 `DeviceStateAdapter<TDevice>` 子类，
   用 `[DeviceAdapter("<key>", typeof(XxxDevice))]` 声明 device key；`Load` / `Save` 通过 `ITagStore`
   与 `FlowTagNaming` 搬字段。
3. Mod：不需要额外登记；`DeviceAdapterCatalog` 启动扫描会建立
   `key → 装置类型 → 适配器`。未发现对应 key / 适配器时直接抛错。
4. Mod：`DeviceRegistry` 订阅事件、`DeviceStateSync` 标脏并在帧末 flush；
   `ItemTagCommit.Commit`（内部先 `SyncModifiedState`）由 `DeviceStateSync` 统一调用。
5. `items.json`：声明 `device` key、anchor、附件。
6. 测试：`DeviceAdapterCatalog` 类型测试 + 适配器纯内存 `FakeTagStore` 测试；
   涉及 tag / 读档的改动按 README §9 做实机验收。

## 7. 不变量与非目标

- **不做防御式代码**：缺 tag 读 0；零值不创建 tag；生命周期由绑定 / 注销保证。
  不为 null、无效状态、不可能分支加 try/catch 或兜底。
- **不在每帧轮询 tag**：装置变化走事件 + `FlowDriver.Update` 末尾的 dirty flush；
  游戏侧变化走快照刷新后的 `DeviceStateReconcilePatch` → `Load`。
- **不缓存第二份属性清单**：属性、槽位全部从 `FlowPropertyRegistry` 派生；
  tag 名缓存 `FlowPropertyTags` 只是命名投影，仍由注册表与 `FlowTagNaming` 生成。
- **不删除原生液体 tag**：零值保留，避免破坏游戏容器语义。
- **不在 Logic 做数值钳制**：脏数据由边界层负责；当前 `FlowStock` /
  `GameItemTagStore` / 适配器只做转换。
- **不把装置状态塞进 `FlowProperties`**：流源 / 容器协议与装置私有状态是两套契约。
