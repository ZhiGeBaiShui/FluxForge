# Mod 层目录规范

> 装置状态与 tag 映射的最终边界见
> [docs/device-state-architecture.md](../../docs/device-state-architecture.md)。

每个类按**职责**归入一个顶层目录。新增类时按下面的判定顺序逐条问，第一个「是」就是它的归属。

## 判定顺序

1. 它是**生命周期事件**——改变物品在网格中的位置/归属 → `GridBuilding/Lifecycle/`；
   存档边界或进程生命周期 → `Bootstrap/`（例外：`EmporiumDestroyed` 挂在商城主场景销毁上，
   跟随网格生命周期放 `GridBuilding/Lifecycle/`）
2. 它属于**物品出生**（定义 → 工厂 → 附件装配）→ `ItemSpawn/`
3. 它属于**网格映射或驱动**（游戏网格 → 逻辑网格、每帧推进）→ `GridBuilding/`
4. 它是 **tag 映射**（流源/容器 tag ↔ `FlowProperties`）→ `TagMapping/`
5. 它是**装置状态适配器**（装置公开属性 ↔ tag）→ `DeviceState/`
6. 它管理**装置实例** → `DeviceServices/`
7. 它是**配置模型或展示模型**（items.json / sprites.json / tooltip 行）→ `Content/`
8. 它创建或更新 **Unity UI 对象** → `Visual/`
9. 它是**游戏/物品适配工具**（坐标 / 方向 / tag 提交）→ `Common/Game/`、`Common/Item/`
10. 它是**对原版的功能接入 patch** → 跟随所属功能目录（tooltip / sprites / device state）
11. 它是 **mod 进程入口或每帧宿主** → `Bootstrap/`
12. 它是无状态、无依赖的工具类 → `Common/<场景>/`

顺序不能换：`GridCreatedPatch` 虽然带 `[HarmonyPatch]`，但它属于「网格出现」这个生命周期事件，所以落 `GridBuilding/`；patch 不单独建顶层目录。

## 目录

### `Bootstrap/` — 进程入口

| 文件 | 职责 |
|---|---|
| `FFModEntry.cs` | `IMod` 入口：初始化、订阅、启停宿主；只做接线（生命周期 #9，`OnDisable` → `FlowDriver.Shutdown` + `FlowVisualLayer.Shutdown`） |
| `ModHost.cs` | 每帧宿主（`Update` / `LateUpdate` / 场景卸载，生命周期 #8） |
| `SaveExited.cs` | 生命周期 #5：退出存档（`EscapeUIManager.OnMainMenu` + `GameMaster.QuitToMenu`）→ `FlowDriver.Shutdown`；不能挂 `PlayerStore.Reset`，它会在构造/预读档时误清网格 |
| `SaveLoaded.cs` | 生命周期 #6：读档完成 → `Bind` + `Rebuild` |

### `ItemSpawn/` — 物品出生

| 路径 | 职责 |
|---|---|
| `Setup/ItemSetup.cs` | 入口：装配件调度（反射发现 + 名字查表） |
| `Services/Factory/ItemFactory.cs` | 工厂：把 `FlowItemDefinition` 的通用属性 / 贴图 / 文本 / 类型 / tag 装配到已存在的 `GameItem`（不创建物品） |
| `Services/Attachments/Base/ItemAttachment.cs` | 附件基类 |
| `Services/Attachments/*` | 各能力附件（device / emitter / flow_container / water_bottle） |

工厂、附件、调度是一个整体：新增一种能力 = 加一个附件 + 在 items.json 声明。

### `GridBuilding/` — 网格映射与驱动

| 路径 | 职责 |
|---|---|
| `Core/FlowDriver.cs` | 入口：遍历所有网格，推进流源与视觉；`Reset` / `Shutdown` 的清理边界见生命周期表 |
| `Lifecycle/GridCreatedPatch.cs` | #1 网格创建：全部 public 构造函数 + 全部 public `SetShape` 重载 → 注册，保证所有构造都被捕获 |
| `Lifecycle/GridLifetime.cs` | #7 网格注销：哨兵节点销毁 → 注销 |
| `Lifecycle/ItemAttached.cs` | #2 物品进入：`AddParent` → `Bind` + `Register` |
| `Lifecycle/ItemDetached.cs` | #3 物品离开：`RemoveParent` → `Unregister` |
| `Lifecycle/ItemPickedUp.cs` | #3b 物品拿起：`StartDrag` → `Unregister` |
| `Lifecycle/InventorySorted.cs` | #4 排序：`Sort` → `Rebuild` |
| `Lifecycle/EmporiumDestroyed.cs` | #5b 商城场景销毁 → `FlowDriver.Shutdown`（退档兜底，2026-11 新增） |
| `Services/GridRegistry.cs` | `GameGridInventory` ↔ `FlowRuntime` |
| `Services/FlowRuntime.cs` | 一块网格的运行环境（流源 + 运动 + 视觉），`Emit` 也从这里发起 |
| `Services/FlowGridAdapter.cs` | 网格 → `IFlowGrid`：anchor 格表缓存 |

完整生命周期总表与反向索引见 [`GridBuilding/Lifecycle/README.md`](GridBuilding/Lifecycle/README.md)。

### `TagMapping/` — 流源 / 容器 tag 映射

| 路径 | 职责 |
|---|---|
| `Core/FlowStock.cs` | 入口：流源 / 容器 `FlowProperties` ↔ 原生液体 tag；不碰装置私有状态 |
| `Core/GameItemTagStore.cs` | `ITagStore` 实现：读 `modifiedState`、写 `state`，等值/零值跳过 |
| `Definitions/FlowTags.cs` | 纯定义：液体容器身份 + anchor 等非流源 tag 常量 |

流源属性 tag 名由 `DeviceState/Naming/FlowTagNaming.cs` 定义；`FlowPropertyTags` 启动时
按注册表生成一次只读缓存，`FlowStock` / 适配器热路径直接读，不再重复拼字符串。
不再有 `FlowTagMap` 映射表。

### `DeviceState/` — 装置状态适配器

| 路径 | 职责 |
|---|---|
| `Contracts/DeviceStateContracts.cs` | `ITagStore` / `DeviceStateAdapter`：装置状态的最小读写契约 |
| `Naming/FlowTagNaming.cs` | 流源属性 id + 种类 → 原版 tag 名；唯一命名规则 |
| `Naming/FlowPropertyTags.cs` | 启动时生成一次属性 → tag 名只读缓存；热路径不拼字符串 |
| `Catalog/DeviceAdapterCatalog.cs` | 启动扫描 `[DeviceAdapter]` 子类；`device key → 装置类型 → 适配器` 自动登记 |
| `Adapters/*StateAdapter.cs` | 每个有状态装置一个适配器；`Load` / `Save` 只搬自己那几个字段 |
| `Sync/DeviceStateSync.cs` | 事件 → 标脏 → 帧末批量 `Save`；`Exhausted` 先同步 flush；有变化才 `ItemTagCommit.Commit`（内部先 `SyncModifiedState`） |
| `Sync/DeviceStateReconcilePatch.cs` | `ValidateShapeState` postfix → `DeviceStateSync.Load(host)`，只回灌不写回 |

适配器不依赖 `DeviceRegistry`、不刷新快照、不做生命周期；组合由
`ItemDeviceBinding` / `DeviceRegistry` 完成，事件订阅在 `DeviceRegistry`，
标脏与 flush 在 `DeviceStateSync`。游戏侧改 tag 后的回灌由 `DeviceStateReconcilePatch`
（`ValidateShapeState` postfix，只 `Load`）负责。

### `DeviceServices/` — 装置服务

| 文件 | 职责 |
|---|---|
| `Core/DeviceRegistry.cs` | 存储：只接收已构建的 `DeviceHost`；`GameItem → DeviceHost` 与装置反查 |
| `Core/DeviceHost.cs` | 唯一构建入口 `Create`：为装置创建适配器并 `Load`；同时聚合 Router / Reactor / 适配器与事件回调 |
| `Core/ItemDeviceBinding.cs` | 建立：物品 tag → 装置角色集合 → 适配器 `Load` → 一个 DeviceHost |
| `Audio/DeviceAudioService.cs` | 消费 `FlowAudio.Played`，语义 ID → 实际音效 |
| `Animation/DeviceAnimationService.cs` | 消费 `FlowAnimation.Played`，当前为空壳 |

装置没有独立的生命周期 patch——建立由「物品进入」(#2) / 「读档完成」(#6) 触发。
`DeviceRegistry` 订阅 `IFlowNotifier.Changed` / `IFlowNotifier.Exhausted`；`DeviceStateSync` 只做标脏、
帧末 flush 与 `Exhausted` 的同步回写。
音效和动画由 Logic 的 `FlowAudio` / `FlowAnimation` 事件驱动，`DeviceHost` 只负责角色聚合。

**注意**：物品被销毁时没有对应的 patch（`ItemDestroyed` 评估后未采用），`DeviceRegistry` 的条目只在
`FlowDriver.Reset` / `Shutdown` 或装置 `Exhausted` 时清理；这是有界保留，详见
`docs/review-pending-fixes.md` P2-2。

### `Content/` — 配置与展示模型

| 路径 | 职责 |
|---|---|
| `Items/FlowItemRegistry.cs` | items.json 解析 + `FlowItemDefinition` |
| `Items/ItemTypeLocalization.cs` | 物品类型显示名（运行时补本地化表） |
| `Tooltip/*` | tooltip 状态行模型 + 渲染 helper |
| `Tooltip/FlowTooltipPatch.cs` | `GetTooltipBasic` → 追加流源状态行 |
| `Sprites/*` | sprites.json 解析 + 贴图缓存 |
| `Sprites/LoadFromAtlasPatch.cs` | `RenderHandler.LoadFromAtlas` → 自定义图集 |

### `Visual/` — 渲染

| 路径 | 职责 |
|---|---|
| `Core/FlowVisualLayer.cs` | 每网格的视觉视图：流源快照 → 绘制项 |
| `Internal/FlowAtlasLayer.cs` | 图集层的 Image 池与像素对齐 |
| `Internal/FlowAtlasTexture.cs` | 图集贴图 / Sprite 缓存 |
| `Internal/FlowParticleRenderer.cs` | 粒子层 |

`Internal/` 表示「只给 `Visual/` 内部用，外部不要直接引用」。

### `Common/` — 无状态工具

| 路径 | 职责 |
|---|---|
| `Game/FlowFacingAdapter.cs` | 游戏 `orientation` → 方向码 |
| `Game/FlowGameSpaceAdapter.cs` | 像素坐标 ↔ 网格坐标 |
| `Item/ItemTagCommit.cs` | tag 写入后的统一提交：`SyncModifiedState` + 容器/贴图表现刷新 |
| `Resources/EmbeddedResources.cs` | 从程序集读嵌入资源 |
| `Serialization/SystemTextJsonCompat.cs` | JSON 兼容层 |

工具类按作用的场景分组；`FlowFacingAdapter` / `FlowGameSpaceAdapter` 是游戏适配，
不是领域模型；`ItemTagCommit` 是物品 tag 提交，不属于 `TagMapping`。

## 命名空间

所有类统一在 `FluxForge.Mod` 命名空间下。目录只表达职责，不参与命名——移动文件不需要改 `namespace` 或 `using`。
