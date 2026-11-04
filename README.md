# FluxForge

> **面向开发者的架构与协作说明。**
>
> FluxForge 是《Probably Stolen》0.46 的 **Mono mod**，由“流源属性系统 + 装置 + 可扩展的产物管线”组成。
> 它使用游戏官方的 mod 加载器，但不是游戏官方认可项目。
>
> **状态：逻辑层有单元测试；适配层只做了构建与代码评审；视觉层是 AI 设计/生成，尚未完成人工设计检阅。**
> 视觉相关代码和资产请按“实验性”对待，不要当作稳定美术规范。

---

## 1. 项目定位

FluxForge 不是背包 mod，也不只是液体运输 mod。它包含三件事：

1. **流源属性系统**：36 种液体成分 + 温度等标量属性，统一注册、读写、分流、汇聚；
2. **装置系统**：冰块、收集器、分流器（Logic 已有 `ice_block` / `collector` / `turn` / `l_right_down` / `t_junction` / `double_emit`；items.json 目前只做了冰块、收集器、L 型转向三件），发射器是“能发射的容器”，不是装置；
3. **可扩展的创造管线**：后续允许玩家以手动或工厂自动化方式，把液体属性与奖励分组合成自定义产物（调酒、药剂、材料等）。

当前仓库包含：

- 纯 C# 逻辑层（`src/Logic`）；
- 游戏适配层（`src/Mod`）；
- 纯表现计算层（`src/Presentation`）；
- 官方 `manifest.xml`、`Textures/Items`、`assets/items.json`；
- 单元测试（只编译 Logic + Presentation）。

---

## 2. 分层总览

```text
┌─────────────────────────────────────────────┐
│ src/Mod（适配层 + Bootstrap + Patches）      │
│  GameItem / TagSystem / Unity / Harmony      │
└───────────────┬─────────────────────────────┘
                │
      ┌─────────┴─────────┐
      ▼                   ▼
┌───────────────┐   ┌───────────────────────┐
│ src/Logic     │   │ src/Presentation      │
│ 纯 C# 规则/状态│   │ 纯 C# 颜色/粒子/图集   │
└───────────────┘   └───────────────────────┘
```

**依赖方向：**

- `Mod → Logic`
- `Mod → Presentation`
- `Logic` 与 `Presentation` **互不依赖**，都是纯 C#；
- `Logic` / `Presentation` 不引用 `UnityEngine`，也不引用 `Assembly-CSharp`；
- 只有 `Mod` 和 Harmony patch 知道游戏类型。

测试工程只编译：

```text
src/Logic/**  +  src/Presentation/**
```

因此逻辑与表现计算可以在没有游戏的机器上直接跑测试。

---

## 3. 逻辑层设计

### 3.1 属性注册表

`FlowPropertyRegistry` 是属性清单的唯一来源：

- **36 种液体**：`water`、`heavy_metal`、`organic_waste` …… `nomore`；
- **1 个标量**：`container_temp`（对应游戏 `CONTAINER_TEMP_TAG`）。

每个 `FlowPropertyDef` 包含：

| 字段 | 含义 |
|---|---|
| `Id` | 稳定标识，如 `water` |
| `Kind` | `Liquid` / `Scalar` |
| `Split` | 分流规则：`Divide` / `Keep` |
| `Merge` | 汇聚规则：`Add` / `WeightedAverage` |
| `Key` | `FlowPropertyKey`，1 基槽位 |

当前规则由 `Kind` 固定：

- 液体：`Divide + Add`；
- 标量：`Keep + WeightedAverage`。

> 注意：`Build()` 内部写死一个局部 `liquids` 字符串数组，并给全部液体统一套 `Divide + Add`、给标量套 `Keep + WeightedAverage`；`FlowProperties` 只按 `def.Split` / `def.Merge` 执行。
> 想新增“同一 Kind 但不同规则”的属性，需要让 `Build`/`FlowPropertyDef` 支持逐属性指定规则；只有新增规则枚举值时才改 `FlowProperties`。

### 3.2 `FlowProperties`

`FlowProperties` 是槽位数组，不是 36 个字段：

```csharp
private readonly double[] _values = new double[FlowPropertyRegistry.Count];

public double Get(FlowPropertyKey key) => _values[key.Index - 1];
public void Set(FlowPropertyKey key, double value) => _values[key.Index - 1] = value;
```

- `FlowPropertyKey` 是 1 基 `readonly struct`；默认值（Index=0）会自然越界；
- `Split` / `Merge` / `SubstanceTotal` 都遍历注册表，不写死属性名；
- `Clone` 用 `Array.Copy` 深拷贝，禁止 `MemberwiseClone`（否则各份共享同一数组）；
- `Split` 对 `Divide` 槽除以份数，`Keep` 槽原样；
- `Merge` 对 `Add` 槽求和，对 `WeightedAverage` 槽按 `SubstanceTotal` 加权；权重为 0 时保留第一份的值；
- `SubstanceTotal` 只累加 `Kind == Liquid`，温度不计入。

### 3.3 `IFlowState`

所有能读写流源属性的对象实现同一个接口：

```csharp
public interface IFlowState
{
    IReadOnlyList<FlowPropertyKey> Keys { get; }
    double Get(FlowPropertyKey key);
    void Set(FlowPropertyKey key, double value);
}
```

- `FlowProperties`：完整 37 槽；
- `IceBlockDevice`：只认 `Water` / `Temperature`（`Keys` 只声明这两个），其他槽读 0 / 写忽略；
- `CollectorDevice`：委托给内部完整 `FlowProperties`，`Keys` 返回全部 37 槽。

`FlowStock` 只依赖 `IFlowState.Keys`，不需要为每种装置写分支。**发射不是装置**：`flow_emitter` 没有装置实例，发射由 `FlowRuntime.Emit` 直接完成。

### 3.4 装置接口与运动层

装置不是只实现 `IFlowState` 就能动。运动层通过两个接口调用它们：

| 接口 | 作用 |
|---|---|
| `IFlowDevice.TryReact(FlowEntity)` | 反应类：读完整流源（属性/位置/速度），返回是否截停 |
| `IFlowRouter.Route(FlowEntity, FlowSpawner, x, y)` | 路由类：改去向 / 数量，返回是否截停/接管（吸收只是其中一种） |
| `IFlowNotifier` | `Changed` / `Exhausted` 播报 |
| `IFlowGrid` | 运动层只读网格查询 |

`FlowManager` 推进时按格探测：先反应装置，再路由装置。装置必须通过 `DeviceRegistry.Register` 登记，`FlowManager` / `FlowGridAdapter` 才能找到它。

已知装置（`FlowDeviceCatalog` 的 key）：

- `IceBlockDevice`（`ice_block`）：
  - 每次最多带走**流源水量的 50%**，且不超过冰块剩余量；
  - 只有水参与融化判定，纯油/其它液体不会融冰；
  - 冰水温度按混合前**总液体质量**加权；
  - 化完先 `Changed` 再 `Exhausted`。
- `CollectorDevice`（`collector`）：`Merge` 累积属性，不保留流源记录；`Exhausted` 永不触发；
- `SplitDevice` 变体（`turn` / `l_right_down` / `t_junction` / `double_emit`）：按**所有非入口通道的 emitCounts 之和**等分；`total == 1` 时只改方向、不分裂；入口通道的 emitCounts 被忽略；
- `FlowManager` / `FlowSpawner`：逐帧推进与生命周期；
- `src/Logic/Reactions/Formulas/`：`TemperatureChangeReaction`、`FilterReaction`，由装置传入参数；原版萃取反应已实现在 `Realtime/Rules`；

逻辑层只校验既有参数契约：

- `FlowProperties.Split(count < 2)` → `ArgumentOutOfRangeException`
- `FlowProperties.Merge` 的 null 参数 → `ArgumentNullException`
- 默认 `FlowPropertyKey` → 越界
- `SplitDevice` 构造参数校验

**逻辑层不负责校验 tag 里的脏数据**（NaN、负液体量、超 int 范围）。这些应在进入逻辑层之前，由适配层/调用方保证；当前 `FlowStock` 只做槽位转换，没有做范围钳制。

---

## 4. 适配层设计

### 4.1 加载顺序与入口

游戏官方加载器的顺序是：

```text
ModLoader 读取 manifest
  → 加载 DLL
  → 创建 Harmony 并 PatchAll(assembly)
  → IMod.Init(manifest)
  → IMod.OnEnable()
```

因此 patch 不是 `FFModEntry` 自己装的；如果 patch 失败，`Init`/`OnEnable` 不会执行。

`FFModEntry : IMod`：

- `Init`：绑定 `ModLog`、`ItemSetup.Initialize()`（反射发现装配件）、`FlowTooltipHelpers.EnsureInitialized()`、`ItemTypeLocalization.RegisterAll()`；
- `OnEnable`：订阅 `ModHook.OnModItemDirectoryInit`、`SaveLoaded.Subscribe()`（`ModHook.OnGameLoadedLate`），创建 `ModHost`；
- `OnDisable`：退订上述两项、停止宿主、`FlowDriver.Shutdown()`、`FlowVisualLayer.Shutdown()`。

`ModHost` 是隐藏 `MonoBehaviour`，因为 `IMod` 没有每帧回调：

- `Update → FlowDriver.Update`
- `LateUpdate → FlowDriver.LateUpdateVisuals`
- `sceneUnloaded → FlowDriver.Reset`（场景切换，保留网格登记）

退出存档走 `SaveExited`：`EscapeUIManager.OnMainMenu`（普通 Escape）+ `GameMaster.QuitToMenu`（团灭/调试）Postfix → `FlowDriver.Shutdown`。
不能挂 `PlayerStore.Reset`：它同时被 `PlayerStore` 构造函数和 `PreLoadStore` 调用，会在 `EmporiumEntry.Start` 构造展示窗/柜台网格时误清 `GridRegistry`。
`EmporiumEntry.OnDestroy` 另有一个场景级兜底（`EmporiumDestroyed`），同样调用 `Shutdown`。

### 4.2 物品目录注册

`OnModItemDirectoryInit` 触发时（`ModItemDirectory.InitDirectory` 里 Fire）：

```text
RegisterInto(directory)
  → directory.Add(key, factory)
  → factory = () => ItemSetup.Create(def)
```

**没有** `OnSceneLoaded` / `FindObjectsOfType<ItemDirectory>()` 兜底：mod 在游戏初始化前加载，订阅一定早于触发；钩子没触发就说明加载顺序变了，属于需要排查的问题，不应静默补注册。

物品创建链（顺序固定）：

```text
FlowItemRegistry（读嵌入的 assets/items.json）
  → ItemSetup.Create(def)
      → ItemDirectory.CreateEmptyItem(null)   // 空白物品
      → ApplyAttachments(item, def)           // 按 items.json 顺序挂附件
      → ItemFactory.Apply(item, def)          // 通用属性 / 贴图 / 文本 / 类型 / tag
  → ItemAttachment.Attach
```

### 4.3 属性 → tag 映射

`FlowTagMap` 在启动时生成 37 条 `Binding`：

| 属性 | tag 规则 | 槽位 |
|---|---|---|
| 液体 | `CURRENT_PART_<ID大写>` | `Int` |
| 标量 | `<ID大写>_TAG` | `Double` |

例如：

```text
water          → CURRENT_PART_WATER        int
container_temp → CONTAINER_TEMP_TAG        double
```

**当前实现按 `FlowPropertyKind` 分支选槽：** Liquid → `Int`，Scalar → `Double`。这是现状，不是通用契约。新增属性时：

- 液体：贴游戏 `currentPart` 的 int 槽；
- 标量：先确认游戏实际写入方用的是 double / int；当前 `SlotKind` 只支持 `Int`/`Double`，float/long 不支持。若游戏实际不是 double，必须同时改 `FlowTagMap.Build`、`SlotKind`、`FlowStock` 读写分支和 `statusLines.valueType`。

### 4.4 tag 语义：单位、两个 TagSystem、FlowStock

#### 单位

- `CURRENT_PART_*` 是 **int µL（0.001 mL）**，不是 mL；
- `CONTAINER_TEMP_TAG` 是 **double 摄氏度**；
- `items.json` 的 `capacityMl` 由 `WaterHelper.InitLiquidContainerItem` 转成内部 µL；
- `statusLines` 的 `scale: 0.001` 用于 µL → 显示单位。

#### `state` 与 `modifiedState`

`GameItem` 有两个 `TagSystem`：

- `state`：基础 tag；
- `modifiedState`：加上容器/形状修正后的快照。

约定：

- **读生效值用 `modifiedState.dict`**；
- **写基础值用 `state.GetTag / ModifyTag`**；
- 一批写完后调用 `item.SyncModifiedState()`，否则 `IsTag`、tooltip、`WaterFeature` 仍读旧快照。

#### `FlowStock` 的职责

`FlowStock` 是**运行期装置 ↔ tag** 的唯一同步入口，不是所有 tag 访问的唯一入口：

- 创建期：`ItemFactory.ApplyTags` 直接写 `state`；
- tooltip：`StatusLineTooltipHelper` 直接读 `modifiedState`；
- 运行期：`FlowStock.Read / ReadInto / Write`。

行为：

- 读：遍历 `target.Keys`，从 `modifiedState.dict` 取值；缺失按 0；
- 写：遍历 `source.Keys`，按槽位 `SetInt` / `SetDouble`；
- 缺失且量化值为 0 → 不创建 tag；
- 缺失且非零 → `GetTag` 创建 + `Enable`；
- 写完后 `SyncModifiedState`，再 `Refresh` 一次；
- 发射后的清空也走 `FlowStock.Write`：`FlowRuntime.Emit` 先 `FlowStock.Read(item)` 取整份存量，再复制一份把液体槽归零、温度保留，写回 37 槽。没有 `EmitterDevice` / `EmitAll` / `Take` 这样的中间对象。

写回时的 `Refresh` 行为：容器走 `WaterFeatureHelper.OnLiquidContainerPourOutOf` + `SpriteHelper.UpdateWaterContainerSprite`；非容器只 `SetSprite` 标记贴图重取。

创建期 tag 同步：`ItemFactory.Apply` 在写完配置 tags 后**统一调用 `SyncModifiedState`**，所以交给目录的物品快照是完整的；装置绑定（`ItemDeviceBinding.Bind`）在物品挂上容器时才按 tag 构造装置并 `FlowStock.ReadInto`，不需要额外的 `Adopt`。`ItemSetup` 不承担状态同步。

### 4.5 运行环境与发射路径

每个网格一个 `FlowRuntime`，**在网格构造/改尺寸时创建**（生命周期 #1），构造时一次装配完：

```text
GridRegistry.Register(GameGridInventory)
  → new FlowRuntime(grid)
      → FlowGridAdapter(grid)     // anchor 格表
      → FlowSpawner()             // 流源名单
      → FlowManager(grid, flows)  // 运动/反应/路由
      → FlowVisualLayer(grid, flows)
```

发射路径（玩家激活）：

```text
EmitterAttachment 三个原生委托
  → MayActivate：父容器是 GameGridInventory？
  → CanActivate：FlowStock.Total(item) > 0
  → Activate：GridRegistry.Emit(item)
      → Get(parentInventory as GameGridInventory).Emit(item)
          → FlowStock.Read(item)            // 发射前重读 tag
          → ResolveEmit：modifiedShape.minX/minY + orientation
          → Grid.Contains(origin)
          → Flows.Accept(new FlowEntity(...))
          → FlowStock.Write(item, cleared)  // 液体归零、温度保留
```

关键点：

- **没有 `Grid.Refresh`、没有 `Adopt`、没有 `EmitterDevice`**：格表在物品进入/离开/拿起/排序/读档时增量维护（见生命周期表）；发射只查询。
- **发射前必须重读 tag**：游戏会直接改液体 tag（`WaterHelper.Add` / `Remove`、倒水等），不经过任何装置事件，缓存的存量会过期。
- **空库存不发射、不扣量**：`FlowStock.Total(item) <= 0` 直接返回 `false`。
- **`ResolveEmit` 读 `modifiedShape` 而不是 `shape`**：后者是定义形状，`minX/minY` 不随拖放变化。
- **`FlowRuntime` 与网格同生命周期**：`GridRegistry.Emit` 直接 `Get(parent).Emit(item)`，取不到 runtime 会 NRE；正常路径由 #1 保证。`ItemAttached` 仍保留一次兜底注册：正常情况下构造期已注册，查不到只可能是登记被整体清空而网格还没销毁（例如退档后旧网格仍存活），此时现场补登一次。

### 4.6 装置变化如何上传

```text
装置修改状态
  ├─ Changed  → FlowDeviceEvents.OnChanged
  │             → FlowStock.Write(item, device)
  │             → tag 更新 + SyncModifiedState + Refresh
  └─ Exhausted → DeviceRegistry.Unregister(item)
                → item.parentInventory?.Expel(item)

装置动作
  → Logic 装置调用 FlowAudio.Play / FlowAnimation.Play
  → Mod 的 DeviceAudioService / DeviceAnimationService
      ├─ Source → GameItem
      ├─ 音效 ID → AudioHelper 音频
      └─ 动画层当前为空壳
```

- 订阅 / 退订只由 `DeviceRegistry.Register` / `Unregister` / `Clear` 负责（`IFlowNotifier : IFlowState`），别处不要手写 `FlowDeviceEvents.Subscribe`；
- 冰块化完会从网格消失（`Exhausted` → `Unregister` + `Expel`）；
- Collector 的 `Exhausted` 是空访问器，永不触发；
- 物品被卖掉/移除时必须先 `Unregister`，否则在途流源可能写到已离场物品（当前已知窗口）；
- **没有 `ItemDestroyed` patch**：物品销毁不会自动注销 `DeviceRegistry`。条目只在 `FlowDriver.Reset` / `Shutdown` 或装置 `Exhausted` 时清理，属于有界保留（见 `docs/review-pending-fixes.md` P2-2）。

### 4.7 物品装配件

`items.json` 的 `attachments` 按顺序执行（当前共 4 个）：

| 装配件 | 作用 |
|---|---|
| `device` | **只写 tag，不创建装置**：把 `device` 字段写成同名 tag（装置类型名），并写 `FLUXFORGE_DEVICE_ANCHOR_X/Y` 两个中心格 tag；`ItemDeviceBinding.Bind` 在物品挂上网格时按这些 tag 现场构造并登记装置 |
| `flow_container` | 调 `WaterHelper.InitLiquidContainerItem(item, capacityMl, 0)`，建立 36 个液体 tag + 温度 + 容器身份 |
| `water_bottle` | 同上，但走透明容器参数（`isTransparentContainer: true`），对齐原版小瓶水 |
| `emitter` | 只接游戏原生激活菜单（3 个 `*ActivateSlotItemFunc` 委托），**不创建装置**；发射由 `FlowRuntime.Emit` 完成 |

`ItemAttachment` 硬约束：

1. 无实例字段（实例全局共享）；
2. 无静态可变状态（不要缓存 `GameItem` / `PixelWindow`）；
3. `public sealed`、public 无参构造、不嵌套；
4. 不覆盖 `ItemFactory` 写入的 identifier / 贴图 / shape / 名称 / 说明 / 风味 / itemTypes / value / 配置 tags；
5. 组合是否互斥由 `items.json` 作者保证；
6. 装配件之间不要互相依赖；
7. 需要参数 → 把参数变成 `FlowItemDefinition` 顶层字段，而不是给装配件加构造参数。

> 装配件由 `ItemSetup.Initialize()` 反射发现（`Assembly.GetTypes()` + `Activator.CreateInstance`），
> 新增装配件 = 写一个类；名字重复会让 `Dictionary.Add` 在启动期直接抛异常。

### 4.8 Harmony patch

生命周期 patch 见 [生命周期总表](src/Mod/GridBuilding/Lifecycle/README.md)：`GridCreatedPatch`、`ItemAttached`、`ItemDetached`、`ItemPickedUp`、`InventorySorted`、`SaveExited`、`EmporiumDestroyed`。

非生命周期 patch：

- `FlowTooltipPatch`：在 `GameItemElement.GetTooltipBasic` 末尾追加状态行；
- `LoadFromAtlasPatch`：拦截 `RenderHandler.LoadFromAtlas`，按 `sprites.json` 的 `atlas` 命中后返回自建 ppu Sprite；
- patch 只做转发/追加，不承载业务规则。

**反射与扫描器的约束**：当前代码只对 `typeof(游戏类型)` 直接做反射（如 `AccessTools.Method`、`typeof(GameGridInventory).GetConstructors()`），不把 `Type` 当参数传给通用反射 helper。游戏 `ModScanner` 会把后者判为 `reflection-external`（对一个计算出的类型反射）。`manifest.xml` 的 `<Capabilities />` 目前保持为空——**新增反射时不要引入 `Type` 参数的 helper，也不要新增文件/网络调用**，否则要么被扫描器标红，要么必须声明能力并承担版本兼容。

---

## 5. 视觉层设计（AI 设计，未完成人工检阅）

> **警告：视觉层由 AI 设计/生成，尚未完成人工设计检阅。**
> 逻辑测试通过不代表视觉风格、可读性、色盲友好性、最终资产质量已定稿。
> 当前 `docs/flow-visual/*` 是旧 Il2Cpp/MelonLoader 时期的设计稿，**以当前 Mono 0.46 代码为准**。

### 5.1 渲染架构

不是“每个流源一个 GameObject”。实际是：

- 每块网格一个 `FlowAtlas` 根节点；
- 内部按槽位池化 `Image`：body / edge / accent / wall + 0–6 张残影；
- 每个流源复用这些槽位，`FlowVisualLayer` 只负责：
  - 订阅实体增删、位置/阻挡变化；
  - 生成 `FlowDrawItem`；
  - 更新粒子系统；
  - 在 `RectTransform` 变化时重建层。

`src/Presentation` 负责纯计算：

- `FlowVisualState.ToKey()`：生成图集键；
- `FlowAtlasIndex`：key → tile 坐标与三级降级；
- `FlowPixelMath`：像素/格坐标换算；
- `FlowComposition` / `FlowTint` / `FlowLiquidPalette`：颜色表与混合；
- `FlowTierMap`：温度/含量/状态/方向档；
- `FlowParticle*`：粒子规则、容量、生命周期。

### 5.2 当前 Tier-0 行为

- **颜色**：运行时按 36 种液体的存量 × `colorMultiplier` 加权混合（`FlowVisualLayer.ComputeBodyColor`），逐行对齐原版 `WaterHelper.GetLiquidContainerColor`；无有效成分时用 `FlowLiquidPalette.Default`（水的本体色）；
- **温度**：
  - 静态图集只影响边缘色；
  - 粒子层还会影响速度与寿命（冷 ×0.6 速度 / ×1.2 寿命，热 ×1.6 / ×0.8）；
- **含量**：取 `Properties.SubstanceTotal`；
- **状态**：当前只产出 `Flow` / `Blocked`（枚举里还有 `Split` / `Merge` / `Collect`，但 Mod 层没有生产者）；
- **射程**：恒为 0，不渲染射程点；
- **图集**：目前只有蒙版/染色，没有按成分/温度拆维度；
- **帧率**：12fps。

### 5.3 已知上限与未完成项

| 项 | 当前状态 |
|---|---|
| 每网格流源数 | 最多画 64 个，超出静默截断 |
| 粒子池 | 512 |
| 状态枚举 | 枚举有 5 个值，只有 `Flow` / `Blocked` 有生产者 |
| 射程 | 恒 0 |
| 36 种液体颜色 | 已接入 `FlowLiquidPalette`（36 项 + `colorMultiplier` 染色权重），按存量加权；不是 6 分量近似 |
| 致死/快感 | 仓库里没有字段/数据契约，属于设计想法 |
| 美术验收 | 未完成；需人工审查图集、粒子、色盲/分辨率 |

人工 review 参考：

- `docs/flow-visual/final/ACCEPTANCE.md`（12 条可复跑验收，但注意旧后端背景）；
- `docs/flow-visual/final/REFACTOR-A.md §7`（已知风险）。

### 5.4 稳定 vs 实验

| 范围 | 状态 |
|---|---|
| Logic 注册表 / Split / Merge / 装置规则 | 有单测，可依赖 |
| Adapter tag 同步 / 生命周期 / patch | 构建 + 代码评审；实机读档待验证 |
| Presentation 纯计算 | 有单测，可依赖 |
| Visual 渲染 / 图集 / 粒子 / 颜色 | **实验性，AI 设计，未人工检阅** |

---

## 6. 原版物品逻辑：物品只承载 tag

理解 FluxForge 的关键，是先理解原版《Probably Stolen》的物品模型：

> **物品本身不“会做事”。物品是 tag 的容器；真正功能由游戏框架里的类查询、调用这些 tag 完成。**

具体来说：

- `GameItem` 持有 `TagSystem state` / `modifiedState`；
- `TagState` 有多个并存槽位：`valueInt` / `valueDouble` / `valueString` / `valueBool` 等；
- 液体容器创建时由 `WaterHelper.InitLiquidContainerItem` 写入：
  - 36 个 `CURRENT_PART_<ID>`（int，单位 µL，初始 0）；
  - `CONTAINER_TEMP_TAG`（double，摄氏度）；
  - `LIQUID_CONTAINER_TAG`、容量、价格等；
- 行为由外部类读取这些 tag 再决定：
  - `WaterHelper.GetTotalVolume` 遍历 `Liquid.Liquids`，累加 `CURRENT_PART_*`；
  - `WaterHelper.GetWaterPurity` 用水的 tag / 总 tag 计算纯度；
  - `WaterFeatureHelper.OnLiquidContainerPourOutOf` 重算显示名、价格、扫描结果；
  - `ChemicalHelper` 处理温度、反应；
  - 物品的功能函数（`maySelectSlotItemFunc` / `onThisTargetItemFunc` 等）是外部挂的委托。

FluxForge 沿用同一模型：

- **自定义状态只持久化在 tag 里**；物品身份（identifier/shape）等也参与读档；
- **装置身份也在 tag 里**：`device` 附件把 `device` 字段写成同名 tag，`ItemDeviceBinding.Bind` 在物品挂上网格时按这个 tag 现场构造装置（读档后由 #6 补绑）；`FLUXFORGE_DEVICE_ANCHOR_X/Y` 记录中心格；
- 装置对象只是运行时镜像；
- 创建期 `ItemFactory.Apply` 结束时统一 `SyncModifiedState`；绑定发生在物品进入网格 / 读档完成，不在 Attach 里读 tag；
- 会改状态的装置（冰块/收集器）实现 `IFlowNotifier`：`Changed → FlowStock.Write` 统一写回 `Keys` 声明的槽位；发射不是装置，`FlowRuntime.Emit` 直接 `FlowStock.Read` + 写回；
- 游戏自己的 `WaterHelper` / `WaterFeatureHelper` 仍能读这些 tag。

因此：

- 改物品行为，优先改“谁在读/写 tag”，而不是给物品加新字段；
- 新增属性 = 新增 tag + 映射 + 读写规则；
- 不要把状态只存在装置对象里而不写回 tag，否则存档/读档会丢；
- `LIQUID_CONTAINER_TAG` 决定 `FlowStock.Refresh` 走 `WaterFeatureHelper` + 容器贴图；非容器只 `SetSprite`。

---

## 7. 扩展指南

> 新增属性的完整说明（注册表、液体/标量规则、tag 约定、非游戏属性的限制）见 [`src/Logic/README.md`](src/Logic/README.md)；下面 §7.1 / §7.2 是速查。

### 7.1 新增液体

1. 在 `FlowPropertyRegistry.Build` 的 `liquids` 数组追加 id（保持与游戏 `Liquid.InitLiquid` 的顺序）；
2. 在 `FlowProps` 加同名静态 Key（**必须加**，测试会检查 `FlowProps` 字段数）；
3. tag 自动生成：`CURRENT_PART_<ID大写>`，int；
4. 更新测试里的 `37` / `36 + 1` 断言，以及 `Order_MatchesGameLiquidInit_AndEndsWithTemperature` 中的 `All[35]` / `All[36]` 索引和 `FlowProps` 字段数（当前都是硬编码；改属性必须同步）；
5. 需要显示时在 `assets/items.json` 加 `tags` / `statusLines`；
6. 加测试：Split 守恒、Merge 温度加权、深拷贝独立、`SubstanceTotal` 只含液体。

### 7.2 新增标量

1. 在 `Build` 末尾加 `FlowPropertyDef` 行；
2. id 必须能按 `<ID>_TAG` 拼出游戏 tag（例如 `container_temp` → `CONTAINER_TEMP_TAG`），否则要改 `FlowTagMap.Build`；
3. `FlowProps` 加 Key；
4. 当前只支持 `Int` / `Double` 两种槽位；`Scalar` 一律映射 `Double`。如果游戏实际是 int，改 `FlowTagMap.Build` 的槽位分支即可；如果实际是 float/long，必须同时扩展 `SlotKind`、`FlowStock` 读写分支和 `statusLines.valueType`；
5. 更新测试计数；
6. `statusLines.valueType` 必须与槽位一致（`int` 默认 / `double` / `string`），否则 tooltip 静默读 0。

### 7.3 新增装置

1. 在 `src/Logic/Devices/` 下实现装置类：路由类实现 `IFlowRouter`，反应类实现 `IFlowDevice`；需要状态写 tag 再加 `IFlowNotifier`。构造保持无参（目录工厂/测试用）。分流器的朝向由 `FlowGridAdapter.RegisterCore` 直接写 `SplitDevice.Facing`，不走通用接口。
2. 在 `src/Logic/Devices/FlowDeviceCatalog.cs` 的静态字典里加一行 `key → 工厂`；key 必须唯一。
3. `items.json` 里加 `"device": "<key>"`、`"anchorX"` / `"anchorY"`（中心格局部坐标），并在 `attachments` 里加 `"device"`；需要放进流量机网格的物品还要有 `"itemTypes": ["FLOW_DEVICE"]`；发射类再额外声明 `"emitter"` 附件（只接激活，不创建装置）。
4. 装置状态只在 `Changed` 时由 `IFlowNotifier` + `FlowStock` 统一写 tag；不要在 Attach 里读 tag。绑定发生在物品挂上网格（`ItemDeviceBinding.Bind`）和读档完成（#6），两处都会 `FlowStock.ReadInto`。
5. **`device` key 同时是持久化 tag 名**：改名等于换物品，旧存档里的装置会丢失身份；新增/改名要按读档兼容处理，不能只改 JSON。
6. 新装置补 `FlowDeviceCatalog` 单测；涉及 tag/读档的改动按 §9 做实机验收。
7. 物品被卖掉/移出时必须 `DeviceRegistry.Unregister`；`DeviceRegistry.Register` 已自动订阅/退订，不需要手写 `FlowDeviceEvents.Subscribe`。

### 7.4 新增物品/资产

`assets/items.json` / `assets/sprites.json` 是**编译期嵌入资源**，改完必须重新编译。

最小闭环：

1. `items.json` 加 `key`、`tags`、`attachments`、名称/描述；
2. `sprites.json` 加 `key` / `atlas` / `file` / `shape` / `shapeWidth`；
3. 运行时 PNG 放 `mod/Textures/Items/`（设计源图可放 `assets/`）；
4. 需要行为就写/复用 `ItemAttachment`；
5. `dotnet build` 后复制 `FluxForge.dll` + `manifest.xml` + `Textures/`。

JSON 由 Newtonsoft 兼容层解析，可能接受注释/尾逗号等宽松语法；不要依赖这些扩展，按标准 JSON 写。解析失败会记日志并跳过该定义。

### 7.5 manifest 规范

最小合法骨架：

```xml
<Manifest>
  <ID>fluxforge</ID>
  <Name>FluxForge</Name>
  <Author>...</Author>
  <ModVersion>0.1.0</ModVersion>
  <GameVersions><Version>0.46</Version></GameVersions>
  <Prerequisites />
  <Assemblies><Assembly>FluxForge.dll</Assembly></Assemblies>
  <Capabilities />
</Manifest>
```

- `<Capabilities />` 是必需元素，没有能力也要写空；
- `<Assemblies>` 必须列全，路径不能超出 mod 文件夹；
- 不要打包 `0Harmony` / `MonoMod`（扫描器判 `bundled-framework`）；
- **当前实现一直在用反射**（`AccessTools.Method`、`typeof(GameGridInventory).GetConstructors()` 等），但不声明 `reflection-external`：这些调用只对 `typeof(游戏类型)` 直接取值，扫描器接受；把 `Type` 当参数传给 helper 会被判 `reflection-external`；
- 空能力清单的项目不要引入新的“计算出的类型”的反射、文件或网络调用；确实需要时，先改 manifest 能力并承担版本兼容。

---

## 8. 开发规范

### 8.1 分层红线

1. **不要把游戏类型放进 `Logic` / `Presentation`**：`GameItem`、`TagState`、`UnityEngine` 只能在 `Mod`。
2. **不要在 `Logic` 里写 Unity 生命周期**：每帧驱动由 `ModHost` 负责。
3. **不要让 patch 承载业务规则**：patch 只转发到逻辑/适配层。
4. **逻辑层只校验既有参数契约，不校验 tag 脏数据**：边界层负责把外部值变成有限、合法值；当前 `FlowStock` 还没做数值钳制，新增来源必须自行保证。
5. **跨 mod 联动只支持源码合并到同一程序集**：不提供公共 API/反射桥。官方 `ModScanner` 会把非游戏程序集引用判为 `unlisted-reference`；把计算出的 `Type` 传给反射 helper 会判 `reflection-external`。当前空能力清单只允许前一种都不出现。

### 8.2 数据契约

- **注册表是唯一属性来源**：不要在别处再维护第二份 36 种液体清单；
- **tag 是持久化真相**：装置只是镜像，改状态必须写回 tag；
- **单位是 µL**：`CURRENT_PART_*` 是 int µL，不是 mL；
- **读 `modifiedState`、写 `state`、写完 `SyncModifiedState`**；
- **槽位是存储契约**：当前按 Kind 分支（Liquid→Int、Scalar→Double）；新增属性必须对照原版写入方；
- **mod ID 必须与 `ItemFactory.ModId` 一致**：改 manifest ID 不同步常量，自定义贴图会全部找不到；
- **文件夹名可改；manifest ID 变更要同步常量并重新验证**。

### 8.3 测试与构建

```powershell
# 主工程（需要游戏 Managed 路径）
dotnet build FluxForge.csproj -c Release -p:GameManagedDir="<Game>/Probably Stolen_Data/Managed"

# 单元测试（只需要 .NET 6 SDK/runtime）
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

- 主工程 `netstandard2.1`，引用游戏 Managed 程序集；
- `FluxForge.csproj` 的 `GameManagedDir` 默认是作者机器路径，换机器必须覆盖；
- 测试只编译 `Logic + Presentation`；
- 新逻辑必须有单元测试；
- 修改 `FlowProperties` 的 Split/Merge/Clone 必须跑全量测试；
- 修改 tag 映射/存档路径必须实机读档验证（Mod 层没有单测）。

当前测试数量：**259**。最近一次运行（2026-11-04）：259 / 259 通过，0 失败 0 跳过。以 `dotnet test` 的实际输出为准，不要在文档里维护固定数字以外的手工统计。

---

## 9. 构建、安装与调试

### 构建

```powershell
dotnet build FluxForge.csproj -c Release -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"
```

产物：

```text
bin/Release/
  FluxForge.dll
  manifest.xml
  Textures/Items/*.png
```

### 安装

1. **完全退出游戏**；
2. 删除旧的 `Mods/ProbablyBagMod`（如果存在）；
3. 新建 `Mods/FluxForge/`；
4. 复制 `FluxForge.dll`、`manifest.xml`、`Textures/`；
5. 启动游戏，检查日志：
   - `[FluxForge]: FluxForge 已加载`
   - `[FluxForge]: FluxForge 已启用`
   - `[注册] <成功数>/<总数> 个物品`
   - `[贴图] ...`（如使用自定义贴图）

### 实机验收清单

1. 新档/读档后，冰块、收集器、发射器的 tooltip 数值正常；
2. 发射纯水：只生成 1 个流源，存量归零，读档后不复活；
3. 收集器吸收后，tooltip 当帧更新；
4. 冰块融化/化完消失，读档后状态一致；
5. 拖动/卖掉装置后**无需重新激活**：格表随 `AddParent` / `RemoveParent` / `StartDrag` 立即更新；排序后格表也正确；
6. 非容器物品（冰块/收集器）的存档没有多出 35 个零 `CURRENT_PART_*` tag；发射器由原版 `flow_container` 创建全部 36 个液体 tag，出现零 tag 属正常；
7. 普通 Escape → 主菜单 → Continue：发射器可用，`DeviceRegistry` / `GridRegistry` 在退档时已清空；
8. 团灭/调试 `QuitToMenu` 后 Continue：发射器可用（`SaveExited` 的第二条目标）；
9. 直接销毁 `EmporiumEntry` 的退档路径（`EmporiumDestroyed`）不残留视觉层，重复 `Shutdown` 不抛异常。

已知：`FlowVisualLayer`/视觉层是 AI 设计，未完成人工检阅；实机画面不能作为逻辑正确性的唯一证据。

---

## 10. 许可与分发

- 仓库当前**没有 LICENSE 文件**；在作者补充许可证之前，**作者原创的代码与文档**默认保留所有权利（All rights reserved）。
- 游戏原版素材、第三方素材、AI 生成资产的权利状态不在上述声明范围内，需分别确认；不要直接复制游戏原版资源。
- 如果你想用本源码制作适配版/衍生版，请先通过仓库 Issue 联系作者取得许可：<https://github.com/ZhiGeBaiShui/FluxForge/issues>，并在 README/manifest 中注明：
  - 来源仓库与原作者；
  - 你的修改内容；
  - 你的版本使用独立 manifest `ID` / `Name` / `Author`，不要冒充 FluxForge 官方版本。
- “官方 mod 加载器”指游戏自带的 `ModLoader`，不代表本 mod 是游戏官方项目。

---

## 11. 给其他 AI / 协作者的阅读顺序

1. `src/Logic/Flow/FlowPropertyRegistry.cs`、`FlowPropertyDef.cs`、`FlowProps.cs`；
2. `src/Logic/Flow/FlowProperties.cs`、`src/Logic/Devices/Interfaces/IFlowState.cs`；
3. `src/Logic/Devices/*`（含 `FlowDeviceCatalog`）、`src/Logic/Motion/*`；
4. `src/Mod/TagMapping/Definitions/FlowTagMap.cs`、`src/Mod/TagMapping/FlowStock.cs`；
5. `src/Mod/GridBuilding/Services/FlowRuntime.cs`、`GridRegistry.cs`、`src/Mod/GridBuilding/FlowDriver.cs`；
6. `src/Mod/ItemSpawn/*`、`src/Mod/DeviceServices/ItemDeviceBinding.cs`、`assets/items.json`、`assets/sprites.json`；
7. `src/Mod/Bootstrap/FFModEntry.cs`、`ModHost.cs`、`SaveExited.cs`、`SaveLoaded.cs`；
8. `src/Mod/Visual/*`（最后读，且记住第 5 节的警告）；
9. `tests/FluxForge.Tests/*`。

游戏 API 参考：`docs/游戏机制_API手册.md`。
注意：该手册是旧 Il2Cpp/MelonLoader 时期的记录，类型名里的 `Il2Cpp.` 前缀在 0.46 Mono 下已不需要；以当前程序集为准。

### 必须学习的规范

- **注册表是唯一属性来源**；
- **tag 是持久化真相**；
- **µL 单位**；
- **读 `modifiedState` / 写 `state` / `SyncModifiedState`**；
- **深拷贝而不是共享数组**；
- **patch 要薄**；
- **逻辑层不防御 tag 脏数据**；
- **测试只依赖纯层**；
- **视觉层未定稿**；
- **不要跨 mod 直接引用程序集**。

### 快速自检

```powershell
# 以下命令任选其一：装了 ripgrep 用 rg，否则用 PowerShell 的 Select-String
$files = (Get-ChildItem src/Logic,src/Presentation -Recurse -Filter *.cs).FullName

# 逻辑/表现层不应出现游戏或 Unity 类型
Select-String -Path $files -Pattern "UnityEngine|GameItem|TagState|Il2Cpp"

# 逻辑层不应有 MemberwiseClone
Select-String -Path (Get-ChildItem src/Logic -Recurse -Filter *.cs).FullName -Pattern "MemberwiseClone"

# 代码/资产中不应再有旧项目名（README 里保留旧目录名说明，故排除）
Select-String -Path (Get-ChildItem src,tests,mod,assets -Recurse -File).FullName -Pattern "ProbablyBagMod|probably_bag_mod|PBModEntry|PBLogger"
```

> 仓库没有随包提供 `rg`；上面的 `Select-String` 版本在 Windows PowerShell / pwsh 下可直接跑。

---

## 12. 当前状态与已知限制

- **逻辑层**：36 种液体 + 温度已注册；Split/Merge/Clone/装置规则有单测；`Realtime/Rules` 已移植原版 22 条每秒反应 + 环境降温；
- **适配层**：tag 双向同步、装置绑定（`ItemDeviceBinding`）、发射路径（`FlowRuntime.Emit`）、网格/存档生命周期已实现；`dotnet build` 0 warning / 0 error，实机读档待验证；
- **视觉层**：AI 设计，未完成人工检阅；36 色加权混合已接入，致死/快感叠色未接入；64 流源 / 512 粒子 / 仅 Flow+Blocked / 射程 0 是当前上限；
- **tooltip**：当前只显示水与温度，非水液体暂不显示；
- **跨 mod API**：不提供；联动请源码合并；
- **已知缺口**：设备身份存在 tag 里，`device` key 改名等于换物品；`DeviceRegistry` 没有物品销毁钩子（有界保留）；`ItemAttached` 仍保留一次「退档后网格仍存活」的兜底注册；`GridRegistry.Emit` 取不到 runtime 时会 NRE，靠 #1 的不变量保证；
- **视觉/美术资产**：必须人工 review 后才能作为稳定规范。
