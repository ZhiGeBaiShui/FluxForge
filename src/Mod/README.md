# Mod 层目录规范

每个类按**职责**归入一个顶层目录。新增类时按下面的判定顺序逐条问，第一个「是」就是它的归属。

## 判定顺序

1. 它是**生命周期事件**——改变物品在网格中的位置/归属 → `GridBuilding/Lifecycle/`；
   存档边界或进程生命周期 → `Bootstrap/`（例外：`EmporiumDestroyed` 挂在商城主场景销毁上，
   跟随网格生命周期放 `GridBuilding/Lifecycle/`）
2. 它属于**物品出生**（定义 → 工厂 → 附件装配）→ `ItemSpawn/`
3. 它属于**网格映射或驱动**（游戏网格 → 逻辑网格、每帧推进）→ `GridBuilding/`
4. 它是 **tag 映射**（物品 tag ↔ 属性 / 装置）→ `TagMapping/`
5. 它管理**装置实例** → `DeviceServices/`
6. 它是**配置模型或展示模型**（items.json / sprites.json / tooltip 行）→ `Content/`
7. 它创建或更新 **Unity UI 对象** → `Visual/`
8. 它是**空间映射**（坐标 / 方向）→ `Game/`
9. 它是**对原版的补丁**，且不属于任何生命周期 → `GamePatches/`
10. 它是 **mod 进程入口或每帧宿主** → `Bootstrap/`
11. 它不依赖游戏也不依赖框架 → `Common/`

顺序不能换：`GridCreatedPatch` 虽然带 `[HarmonyPatch]`，但它属于「网格出现」这个生命周期事件，所以落 `GridBuilding/` 而不是 `GamePatches/`。

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
| `ItemSetup.cs` | 入口：装配件调度（反射发现 + 名字查表） |
| `Services/ItemFactory.cs` | 工厂：把 `FlowItemDefinition` 的通用属性 / 贴图 / 文本 / 类型 / tag 装配到已存在的 `GameItem`（不创建物品） |
| `Services/Attachments/ItemAttachment.cs` | 附件基类 |
| `Services/Attachments/*` | 各能力附件（device / emitter / flow_container / water_bottle） |

工厂、附件、调度是一个整体：新增一种能力 = 加一个附件 + 在 items.json 声明。

### `GridBuilding/` — 网格映射与驱动

| 路径 | 职责 |
|---|---|
| `FlowDriver.cs` | 入口：遍历所有网格，推进流源与视觉；`Reset` / `Shutdown` 的清理边界见生命周期表 |
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

### `TagMapping/` — tag 映射

| 路径 | 职责 |
|---|---|
| `FlowStock.cs` | 入口：物品 tag ↔ `FlowProperties` 的唯一读写点 |
| `Definitions/FlowTags.cs` | 纯定义：所有 tag 常量 |
| `Definitions/FlowTagMap.cs` | 纯定义：`FlowPropertyKey` ↔ tag 名 / 槽位类型 |

### `DeviceServices/` — 装置服务

| 文件 | 职责 |
|---|---|
| `DeviceRegistry.cs` | 存储：`GameItem → DeviceHost`，统一管理全部装置角色 |
| `DeviceHost.cs` | 一件物品的 Router / Reactor / Notifier 角色聚合 |
| `ItemDeviceBinding.cs` | 建立：物品 tag → 装置角色集合 → 一个 DeviceHost |
| `Audio/DeviceAudioService.cs` | 消费 `FlowAudio.Played`，语义 ID → 实际音效 |
| `Animation/DeviceAnimationService.cs` | 消费 `FlowAnimation.Played`，当前为空壳 |
| `FlowDeviceEvents.cs` | 订阅注册：装置 `Changed` / `Exhausted` → tag 写回 / 移出容器 |

装置没有独立的生命周期 patch——建立由「物品进入」(#2) / 「读档完成」(#6) 触发。
音效和动画由 Logic 的 `FlowAudio` / `FlowAnimation` 事件驱动，`DeviceHost` 只负责角色聚合。

**注意**：物品被销毁时没有对应的 patch（`ItemDestroyed` 评估后未采用），`DeviceRegistry` 的条目只在
`FlowDriver.Reset` / `Shutdown` 或装置 `Exhausted` 时清理；这是有界保留，详见
`docs/review-pending-fixes.md` P2-2。

### `Content/` — 配置与展示模型

| 路径 | 职责 |
|---|---|
| `FlowItemRegistry.cs` | items.json 解析 + `FlowItemDefinition` |
| `ItemTypeLocalization.cs` | 物品类型显示名（运行时补本地化表） |
| `Tooltip/*` | tooltip 状态行模型 + 渲染 helper |
| `Sprites/*` | sprites.json 解析 + 贴图缓存 |

### `Visual/` — 渲染

| 路径 | 职责 |
|---|---|
| `FlowVisualLayer.cs` | 每网格的视觉视图：流源快照 → 绘制项 |
| `Internal/FlowAtlasLayer.cs` | 图集层的 Image 池与像素对齐 |
| `Internal/FlowAtlasTexture.cs` | 图集贴图 / Sprite 缓存 |
| `Internal/FlowParticleRenderer.cs` | 粒子层 |

`Internal/` 表示「只给 `Visual/` 内部用，外部不要直接引用」。

### `Game/` — 空间映射

| 文件 | 职责 |
|---|---|
| `FlowFacing.cs` | 游戏 `orientation` → 方向码 |
| `FlowGameSpace.cs` | 像素坐标 ↔ 网格坐标 |

### `GamePatches/` — 对原版的补丁（非生命周期）

| 文件 | 目标 |
|---|---|
| `FlowTooltipPatch.cs` | `GetTooltipBasic` → 追加流源状态行 |
| `LoadFromAtlasPatch.cs` | `RenderHandler.LoadFromAtlas` → 自定义图集 |

这些补丁的存在只因为原版没有提供扩展点，不属于任何生命周期。

### `Common/` — 无依赖工具

| 文件 | 职责 |
|---|---|
| `EmbeddedResources.cs` | 从程序集读嵌入资源 |
| `SystemTextJsonCompat.cs` | JSON 兼容层 |

## 命名空间

所有类统一在 `FluxForge.Mod` 命名空间下。目录只表达职责，不参与命名——移动文件不需要改 `namespace` 或 `using`。
