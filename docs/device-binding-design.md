# 装置通用绑定与 Split 变体体系（历史设计稿 v0.1）

> 状态：**历史设计记录**（2026-11-01 已实施；保留为决策记录）。**本文不代表当前实现**，当前代码请以 `src/Logic/Devices/`、`src/Mod/DeviceServices/`、`src/Mod/GridBuilding/` 为准。
>
> **当前实现差异（2026-11-04 核对）**：
>
> - **无 `EmitterDevice` / `EmitAll` / `Adopt`**：发射由 `FlowRuntime.Emit` 直接完成（`FlowStock.Read` → 生成流源 → 液体归零写回）；没有 `FlowRuntime._adopted`。
> - **装置身份改为 tag 驱动**：`DeviceAttachment` 只把 `items.json` 的 `device` 字段写成**同名 tag**，并写 `FLUXFORGE_DEVICE_ANCHOR_X/Y` 中心格 tag；不在 Attach 时 `new` 装置。`ItemDeviceBinding.Bind` 在物品挂上网格（#2）和读档完成（#6）时遍历 `FlowDeviceCatalog.Keys`，命中 tag 才构造并登记。
> - **§4.4「构造/类型 key 只放 items.json，不放 tag」已被推翻**：`device` key 现在就是持久化 tag 名，改名等于换物品；旧存档会丢失装置身份。
> - Logic 目录当前 key：`collector` / `ice_block` / `turn` / `l_right_down` / `t_junction` / `double_emit`（**没有 `emitter`**）。
> - Split 常量：`Turn {3,2}`（默认 1 道）、`LRightDown {1,2}`（默认 1 道）、`TJunction {0,1,3}` + `{1,1,1}`、`DoubleEmit {0,2}` + `{2,2}`。
> - 网格登记粒度、朝向同步在 `FlowGridAdapter.RegisterCore`：按 anchor tag + `GridShape.ForwardTransform` 落到一格，并写 `SplitDevice.Facing`；本文提到的 `FlowGridAdapter.Refresh` 已不存在。
> - 测试基线：当前 **221 / 221**（本文 §5/§9 里的 230 是当时基线）。
> - §3.4（adopt 回灌）、§3.6（`DeviceRegistry` 代码片段）、§4.2（items.json 示例）、§5（迁移步骤）保留为历史方案；实际实现见上面列出的文件。

> 仓库：`E:\code\FluxForge`；游戏：Probably Stolen 0.46；框架：官方 Mono mod（`manifest.xml` + `FluxForge.dll`）；目标框架：`netstandard2.1`；脚本后端：Mono（`Assembly-CSharp.dll`）。
> 事实校正：`.modstudio/AGENTS.md`、`TDD.md`、`context.md` 里的 il2cpp / MelonLoader 0.7.3 / net6.0 是旧阶段（流源视觉 v1）的记录，**不是当前事实**；当前构建与加载以 `FluxForge.csproj`、`manifest.xml`、README 为准。
> 评审输入：架构子代理、Split 子代理（两轮）、对抗审查子代理（两轮）、数据/持久化子代理。本文是汇总裁决稿；未修改源码。

---

## 1. 问题

当前每类装置都要写一个 Mod 层 `ItemAttachment`：

| 文件 | 现在做的事 |
|---|---|
| `CollectorAttachment` | `new CollectorDevice()` + `Register` + `Subscribe` |
| `IceBlockAttachment` | `new IceBlockDevice(0,0)` + `ReadInto` + `Register` + `Subscribe` |
| `EmitterAttachment` | `new EmitterDevice(stock)` + `Register` + 激活委托 |

这带来四个问题：

1. 新增装置必须在 Logic 之外再写一个 Mod 类，且类的构造签名各不相同（无参 / 两个 double / `FlowProperties` / 通道数组）；
2. `FlowRuntime.Emit` 里 adopt 写死 `CollectorDevice` / `IceBlockDevice`，新装置读档不会回灌 tag；
3. `Subscribe` 是手工调用，漏写不报错，只表现为“装置反应正常但 tag 不更新”；
4. `SplitDevice` 目前是具体类，通道与发射数只能靠构造参数传；一旦做成物品，没有统一的无参构造方式。

目标：**新增装置 = Logic 类 + Logic 目录一行 + items.json**；Mod 层不再出现装置专属 attachment；装置与适配层的状态交流只走 tag 映射。激活、面板、原版容器仍由物品侧 attachment 负责。

非目标：不引入反射扫描；不做运行期任意配置 DSL；不改变游戏存档格式；不在 `Update`/视觉层里轮询 tag。

---

## 2. 方案对比

| 方案 | 构造方式 | 类型选择 | Mod 层新增成本 | 结论 |
|---|---|---|---|---|
| A 现状：每装置一个 attachment | 各写各的 `new` | attachment 类名 | 一个类/装置 | 保留为回退方案 |
| **B 泛用 `device` attachment + Logic 目录** | 全部无参工厂 | items.json `device` key → 目录 | 0（只在 Logic 加一行） | **推荐** |
| C 反射扫描 `[FlowDevice]` | 全部无参 | 特性 + 程序集扫描 | 0 | 拒绝：项目已有反射/文档漂移的教训，见 §7 |
| D 单类 + `Configure(config)` | 全部无参 | items.json 参数 | 0 | Split 变体 >8 或要玩家自定义时再考虑 |
| E 装置 key 存 tag | 全部无参 | tag 值 | 0 | 拒绝：读档时 `TagSystem.Clone` 在工厂之后，tag 会被旧档覆盖，见 §6 |

**关于“解耦 vs 动态维护”**：两者冲突不在“构造参数”，而在“类型选择”。无参构造只让工厂变简单；`"split_2way"` 对应哪个类仍然需要一行映射。把这一行放进 Logic 目录，Mod 层就能零感知；反射能省掉这一行，但把编译期问题变成运行期问题。

---

## 3. 决策

### 3.1 Logic 装置目录

新增 `src/Logic/Devices/FlowDeviceCatalog.cs`：

```csharp
public static class FlowDeviceCatalog
{
    private static readonly Dictionary<string, Func<object>> Factories
        = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
    {
        ["collector"]       = () => new CollectorDevice(),
        ["ice_block"]       = () => new IceBlockDevice(),
        ["emitter"]         = () => new EmitterDevice(),
        ["turn"]            = () => new TurnDevice(),
        ["t_junction"]      = () => new TJunctionDevice(),
        ["double_emit"]     = () => new DoubleEmitDevice(),
    };

    public static IReadOnlyCollection<string> Keys { get; }
    public static object Create(string key);
}
```

- 返回 `object`，不要求所有装置实现 `IFlowState`：`SplitDevice` 只是 `IFlowRouter`，没有状态。
- 工厂只存委托，不存实例：两件同型物品必须是两个独立装置（否则 `DeviceRegistry` 反向表只认最后一件）。
- 不做未知 key / 重复 key 的兜底：key 由 items.json 保证合法，未知 key 直接由字典索引暴露。
- 与 `FlowPropertyRegistry` 同构：注册表是唯一来源，新增装置只加一行。

### 3.2 唯一的 Mod 装置 attachment

新增 `src/Mod/Items/Setup/Attachments/Device/DeviceAttachment.cs`：

```csharp
public sealed class DeviceAttachment : ItemAttachment
{
    public override string Name => "device";

    public override void Attach(GameItem item, FlowItemDefinition def)
    {
        object device = FlowDeviceCatalog.Create(def.device);
        DeviceRegistry.Register(item, device);
    }
}
```

注意：

- **不在这里读 tag**。`ItemFactory` 创建物品时 `ApplyTags` 写的是 `state`，`modifiedState` 尚未刷新；读档时 `TagSystem.Clone` 又在工厂之后才回灌。Attach 时读 tag 只能读到默认值。状态回灌统一放在首次有效发射的 adopt（§3.4）。
- `def.device` 由 items.json 保证合法；未知 key 直接由 `FlowDeviceCatalog.Create` 的字典索引暴露，不做运行期兜底。
- 分流器的朝向由 `FlowGridAdapter.Refresh` 直接写 `SplitDevice.Facing`（§3.5），这里不处理。
- `DeviceRegistry.Register` 负责 `IFlowNotifier` 的订阅；没有第二个订阅点。

### 3.3 接口契约

1. `IFlowNotifier : IFlowState`
   - 播报者必须能暴露自己的状态，`FlowDeviceEvents.OnChanged` 不再 `(IFlowState)sender` 硬转。
   - 只影响 `CollectorDevice` / `IceBlockDevice`（已经是双接口）；不强制 `SplitDevice` 等路由装置实现。
2. `IFlowState` 仍然只表示“可读写状态”；`SplitDevice` 不实现它。
3. 暂不引入 `IFlowEmitter`、`IFlowConfigurable`、`FlowDeviceConfig`。发射装置继续用具体 `EmitterDevice`；Split 变体用无参子类，不传配置。等出现第二种发射装置或变体 >8 时再抽象。

### 3.4 读档回灌（adopt）泛化

删除 `FlowRuntime.Emit` 里的硬编码分支：

```csharp
// 现在：Get<CollectorDevice> / Get<IceBlockDevice>
// 改为：
for (int i = 0; i < items.Count; i++)
{
    GameItem item = items[i];
    if (item == null) continue;

    IFlowState state = DeviceRegistry.Get<IFlowState>(item);
    if (state != null)
    {
        Adopt(item, state);
    }
}
```

- `Adopt` 内先 `item.SyncModifiedState()`，再 `FlowStock.ReadInto(item, state)`。`TagSystem.Clone` 是否重算 `modifiedState` 尚未实机核实，且 `flow_container` 的写入不在 `ItemFactory.ApplyTags` 的 sync 覆盖范围内；不先 sync 就可能读到 0，随后首次 `Changed` 把 0 写回 tag，抹掉存档。`ItemSetup` 不参与状态同步（保持原样）。
- `Adopt` 继续用 `_adopted` 做“每 runtime 每物品一次”，仍然发生在 `Accept` 之前。
- 发射器也会被 adopt 一次；`EmitFrom` 发射前再用 `FlowStock.ReadInto` 重读 tag，adopt 之后的外部灌装也能被看到。
- 不在 `OnGameLoadedInit` 做回灌：该钩子在读档 IO 之前触发（`docs/游戏机制_API手册.md` §3.7），此时物品还没重建。
- Assembly-CSharp 中存在 `onLoaded` 符号（已用二进制检索确认 3 处），但签名/调用约定尚未实机验证；v1 不依赖它。若后续验证通过，可把 adopt 提前到每个物品 `onLoaded`（Clone 之后），首次 Emit 仍保留 `EnsureAdopted` 兜底。
- 风险：装置换网格时新 runtime 会用 tag 覆盖内存存量；约定“装置移动/卖出前先把状态写回 tag”，或后续加脏标记。

### 3.5 朝向同步

新增 `src/Mod/Flow/Adapters/FlowFacing.cs`（游戏/适配层，不进 Logic）：

```csharp
public static class FlowFacing
{
    public static int FromOrientation(int orientation)
    {
        return (4 - (orientation & 3)) & 3;
    }
}
```

- `FlowRuntime.ResolveEmit` 与 `FlowGridAdapter.Refresh` 共用它。
- `FlowGridAdapter.Refresh` 在展开格子表时，对每个物品取 `DeviceRegistry.Get<SplitDevice>(item)`，用 `shape.orientation` 设置 `Facing`。
- 局限：`Refresh` 只在有效发射时调用；摆好后旋转装置要等下次发射才生效。这与现有“网格快照只在发射时刷新”的已知缺陷同类，不承诺增量维护。
- 不在每帧同步朝向。

### 3.6 设备生命周期与订阅

`DeviceRegistry` 变成唯一的接线点：

```csharp
public static void Register(GameItem item, object device)
{
    _devices[item] = device;
    _items[device] = item;

    if (device is IFlowNotifier notifier) FlowDeviceEvents.Subscribe(notifier);
}

public static void Unregister(GameItem item)
{
    object device = _devices[item];
    _devices.Remove(item);
    _items.Remove(device);

    if (device is IFlowNotifier notifier) FlowDeviceEvents.Unsubscribe(notifier);
}

public static void Clear()
{
    foreach (object device in _devices.Values)
    {
        if (device is IFlowNotifier notifier) FlowDeviceEvents.Unsubscribe(notifier);
    }

    _devices.Clear();
    _items.Clear();
}
```

- `FlowDeviceEvents` 增加 `Unsubscribe`；`Subscribe/Unsubscribe` 只由 `DeviceRegistry` 调用。
- `OnChanged` 直接 `FlowStock.Write(DeviceRegistry.GetItem(sender), sender)`；`OnExhausted` 注销后 `Expel`。不做空值/异常兜底——装置与物品生命周期由调用方保证。
- **`IFlowNotifier` 的状态必须对其 `Keys` 权威**：`IFlowState.Keys` 声明装置拥有的属性，`FlowStock.Write` 只遍历这些 key；未声明的 tag 不会被碰。`Emitter`/`Collector` 声明全部 37 槽，`IceBlock` 只声明水量/温度。
- 这样“注册即可播报”，不会漏订阅；注销/清空都会退订。

### 3.7 SplitDevice 变体

`SplitDevice` 改为 `abstract` 基类，保留现有校验、`Facing` 换算与 `Route` 算法；`Route` 不 virtual。子类只提供常量：

```csharp
public abstract class SplitDevice : IFlowRouter
{
    protected SplitDevice(int[] relativeChannelDirs, int[] emitCounts = null) { ... }
    public int Facing { get; set; }
    public bool Route(...) { ... }
}
```

第一批变体（2 个分流 + 1 个 L 型转向；常量都相对**美术原图**，见下方基准说明）：

| 变体 | 相对通道 | emitCounts | 行为（Facing 0） |
|---|---|---|---|
| `TurnDevice` | `3,2` | 默认 1 | L 型：右进下出；上行→左出；其余方向穿过。`total==1`，是转向不是分流 |
| `TJunctionDevice` | `0,1,3` | `1,1,1` | 三通（枝朝上、缺下口）：向右→上+右，向下→右+左，向左→上+左；向上穿过 |
| `DoubleEmitDevice` | `0,2` | `2,2` | 同口双发：向上→上出 2 道，向下→下出 2 道；两个子流源同格同向同速、完全重合 |

**常量的美术基准**（必须先定，否则“开口与贴图差 180°”）：

- `TJunctionDevice` 的 `{0,1,3}` 表示“枝朝上、缺下口”。如果贴图画成常见 T 形（枝朝下），常量改成 `{3,2,1}`，emits 不变。
- `DoubleEmitDevice` 的 `{0,2}` + `{2,2}` 是双向双发，任何方向进入都不会被吸收。若将来做**单向**双发，必须把 0 放在“入口通道”上（例如顶进底出用 `{0,2}` + `{0,2}`），否则正方向进入会 `total==0` 被静默 Kill；单向版还要配合 Facing 同步，否则旋转后可能吞流。
- `TurnDevice` 的方向表要按真值表测全，不能只测“右进下出”。
- 同口双发的两颗子流源完全重合，视觉上像一道；这是设计事实，不是 bug。
- `CrossDevice`（`0,1,2,3`，实际是三路分流）暂不做：命名与“直行优先”策略未定，等有需求再加。
- 同口双发必须靠 `emitCounts`，不能写重复方向（`ValidateChannelDirs` 禁止重复）。
- 测试用 `AnySplit : SplitDevice` 提供可配构造；产品程序集不放测试钩子。现有 5 处 `new SplitDevice(...)` 改为 `AnySplit` 或真实变体。
- `SplitDevice` 的 `Facing` 由 `FlowGridAdapter.Refresh` 直接写入；通道方向存相对值，字段名从 `_absoluteChannelDirs` 改为 `_relativeDirs`（当前命名与注释矛盾）。

---

## 4. items.json 数据模型

### 4.1 新字段

`FlowItemDefinition` 增加：

```csharp
/// <summary>Logic 装置目录 key；空 = 纯数据物品。</summary>
public string device { get; set; }
```

v1 **不增加** `deviceConfig`：Split 变体用无参子类表达，构造参数不是数据。等出现“同一种装置需要不同参数”的真实需求，再加类型化配置（不要让 JSON 变成弱类型 DSL）。

### 4.2 现有物品迁移

```jsonc
{ "key": "flow_collector", "device": "collector", "attachments": ["device"], ... }
{ "key": "ice_block",      "device": "ice_block", "attachments": ["device"], ... }
{ "key": "flow_emitter",   "device": "emitter",   "attachments": ["flow_container", "device", "emitter"], ... }

// 新增分流物品时，必须同时声明 FLOW_DEVICE 类型，否则放不进流量机网格
{ "key": "flow_splitter", "device": "t_junction",
  "itemTypes": ["FLOW_DEVICE"], "sprite": "flow_splitter", "name": "分流器", ... }
```

- `flow_machine` 不变，仍只有 `["machine"]`。
- 顺序：`flow_container` 放在 `device` 前只是可读性建议（v1 的 `DeviceAttachment` 不读 tag）；`device` 与 `emitter` 的先后不影响激活，因为激活只接委托、到点击时才查装置。
- `EmitterAttachment` 只保留激活委托，不再 `new EmitterDevice`；**不**在 Attach 时校验装置存在（此时可能还没轮到 `device` 附件，顺序无关），未知 device key 直接由 `FlowDeviceCatalog.Create` 的字典索引暴露，激活谓词按实际登记结果判。
- 新 Split 物品还需要贴图、名称、tooltip；`FLOW_DEVICE` 类型是进入流量机网格的白名单条件（`MachineAttachment.AllowOnlyTaggedItem`）。

### 4.3 不做额外校验

`ItemSetup` 只做装配件分派；`DeviceAttachment` 也不做 device key / 附件配对校验。`def.device` 视为 items.json 的作者契约：未知 key 直接由 `FlowDeviceCatalog.Create` 的字典索引暴露，`device` 声明与 attachments 的配对由配置作者保证。

### 4.4 tag 的边界

- 构造/类型 key **只放 items.json**，不放 tag。理由：读档时 `TagSystem.Clone` 在工厂之后，tag 会被旧档覆盖；key 改名等于换物品。
- tag 只存运行期状态（现有 `CURRENT_PART_*`、`CONTAINER_TEMP_TAG`）。
- 若将来确实需要“玩家可改的每实例配置”，按数据/持久化评审建议：
  - 统一 `FLUXFORGE_` 前缀、大写 + 下划线，**不用点号、不混大小写**；`Fluxforge.x1` 这种命名否决。
  - 结构化/变长配置用一个 JSON 字符串 tag（如 `FLUXFORGE_DEVICE_CONFIG`）+ 一个 schema int tag（`FLUXFORGE_DEVICE_SCHEMA`），原子、易迁移；不要把变长数组拆成 N 个 int tag。
  - 配置 tag 必须 `Enable`；存档序列化会丢弃 disabled 且全零的 tag。
  - 运行期改配置：校验 → 原子换数组 → 写 `item.state` → `SyncModifiedState` → 发 `ConfigChanged`；不缓存 `TagState`。
- v1 不做运行期配置；Split 变体用无参子类，不需要 config tag。

---

## 5. 迁移步骤（实施时）

1. **Logic**
   - 新增 `FlowDeviceCatalog`；
   - `IFlowNotifier : IFlowState`；
   - `CollectorDevice` / `IceBlockDevice` 保留现有构造，新增无参构造（`IceBlockDevice()` → `this(0,0)`；`EmitterDevice()` → `Stock = new FlowProperties()`）；
   - `SplitDevice` 改 abstract + `TJunctionDevice` / `DoubleEmitDevice`（`TurnDevice` 视 L 型需求保留）；字段改名。
2. **Mod**
   - 新增 `DeviceAttachment`、`FlowFacing`（游戏 orientation → 方向码，适配层）；
   - 删除 `CollectorAttachment`、`IceBlockAttachment`；`EmitterAttachment` 只留激活；
   - `DeviceRegistry` 自动订阅/退订、`Clear` 退订；
   - `FlowDeviceEvents` 加 `Unsubscribe`；
   - `FlowRuntime.Emit` 的硬编码 adopt 改 `IFlowState` 泛化，adopt 内先 `SyncModifiedState`；
   - `FlowGridAdapter.Refresh` 直接同步 `SplitDevice.Facing`；
   - `FlowItemDefinition` 加 `device`；`ItemSetup` **保持原样**。
3. **数据**：`items.json` 三条加 `device`、调整 attachments。
4. **测试**：新增 `DeviceCatalogTests`；Split 变体测试；现有 186 条保持（Split 测试改用 `AnySplit`/真实变体），实施后总数为 230。
5. **文档**：README §7.3 改为新步骤；本文链接进扩展指南。
6. **构建/单测/实机**：`dotnet build -c Release`、`dotnet test`、实机读档与旋转验证。

---

## 6. 测试与验收

### 6.1 可单测（Logic）

- 目录：每个已知 key 可创建，两次创建不同实例。
- Split 变体：通道/emitCounts 表；Facing 0..3 旋转；T 型两路、同口双发两路；子流源出生格内、20 帧不增长；`AnySplit` 任意配置回归。
- **穿过**：T 向上、Double 向右、Turn 向下 → `Route` 返回 false，位置按正常移动（现有 `Split_NeverTakesControl` 只覆盖 `{1,3}`）。
- **全出口 0 吸收**：`AnySplit({3,2},{1,0})` → `Kill`、名单清空、返回 true。
- **入口 emitCounts 被忽略**：Double 的入口通道写 5、出口写 1，仍只发 1 道。
- **混合计数配对**：`AnySplit({2,1,3},{0,2,1})` → 3 道、2+1 分配（抓常量错位）。
- **Turn 的 `total==1`**：`Assert.Same(incoming)`、位置不变、仅方向变。
- **属性守恒**：两路各 1/2，和 == 原值；Facing 越界抛 `ArgumentOutOfRangeException`；改 Facing 立即生效；`AnySplit` 构造校验回归（重复方向/长度不匹配/<2 通道）。
- 现有 `FlowProperties` / `DeviceTests` / `FlowBlockedStateTests` 全绿。

### 6.2 只能实机

- 新档/读档：collector/ice block 存量不归零、不回升；tooltip 正确。
- 发射器：激活菜单正常、水量下降、温度 tag 随 `Changed → FlowStock.Write` 跨发射保留。
- 旋转 Split 后再次发射：出口方向跟随；不旋转时保持。
- Split 物品的贴图开口与常量基准一致（T 枝朝上/朝下、Double 双向双发），旋转 90° 后仍一致。
- 卖出收集器后再发射：无异常、其它网格不受影响。
- `dotnet build` 0 warning / 0 error；manifest 仍为空能力。

---

## 7. 风险与边界

| # | 风险 | 缓解 |
|---|---|---|
| 1 | Attach 时读 tag 导致读档归零 → 首次 `Changed` 写回 0，抹存档 | **Attach 不读 tag**；adopt 固定在首次有效发射、`TagSystem.Clone` 之后 |
| 2 | `ItemFactory` / `flow_container` 写入后 `modifiedState` 未刷新；adopt 读到 0 | `Adopt` 内先 `item.SyncModifiedState()` 再 `FlowStock.ReadInto`；`ItemSetup` 保持原样，不承担状态同步；这是已知缺口，README §4.4 |
| 3 | device key 笔误/未注册 | 不兜底：`FlowDeviceCatalog.Create` 直接按 key 取工厂；配置作者保证 key 与 attachments 正确 |
| 4 | 目录返回共享实例 | 目录只存工厂；测试断言两次创建不同引用 |
| 5 | 重复注册/双订阅 | `Register` 先退订旧装置；`Clear`/`Unregister` 退订；同一物品只允许一个装置 |
| 6 | 幽灵装置写已卖出物品 | 不做兜底；卖出/移出必须 `Unregister`（列为前置） |
| 7 | 装置旋转后 Facing 不更新 | `Refresh` 重同步（下次发射生效）；与网格快照缺陷同批处理，不做每帧 |
| 8 | 装置移动换网格丢未写回存量 | 约定移动前写 tag；后续加脏标记/移动钩子 |
| 9 | 同时实现 `IFlowDevice` + `IFlowRouter` 会被同格调用两次 | 文档禁止；如需复合装置，先定义明确的执行顺序 |
| 10 | `deviceConfig` 弱类型 DSL | v1 不做；将来加类型化配置 + 校验，不解析 JSON 字符串 |
| 11 | 反射扫描与空能力声明冲突 | 显式目录，不新增反射；`ItemSetup` 现有反射与文档漂移单独处理 |
| 12 | `.modstudio` 旧事实（il2cpp/MelonLoader）误导 | 以 `FluxForge.csproj`/`manifest.xml`/README 为准；本文已注明 |
| 13 | 新 Split 物品缺 `FLOW_DEVICE` 类型/贴图 → 放不进流量机或不可见 | items.json 同时声明 `itemTypes: ["FLOW_DEVICE"]`、sprite、tooltip；实机验收 |
| 14 | 部分状态装置实现 `IFlowNotifier`，`FlowStock.Write` 把其它液体零 tag 抹掉 | `IFlowState.Keys` 声明 owned keys；`FlowStock` 只遍历这些 key，IceBlock 只声明水量/温度，Collector/Emitter 全量 |
| 15 | Split 常量相对美术基准错位；单向双发在旋转/Facing 过期时静默吞流 | v1 用双向双发 `{0,2}/{2,2}`；T 明确“枝朝上”基准；旋转后实机验收开口方向 |

**关于“是否值得做”**：对抗审查子代理的判据是“本轮至少新增 2 个分流变体才值得”。当前用户明确要 Split 两变体 + 通用绑定，满足该判据。若最终只做绑定、不做 Split 物品，则应暂缓 `SplitDevice` 基类改造，只做泛用 attachment + adopt 泛化（减半版）。

---

## 8. 开放决策

1. `DoubleEmitDevice` 采单向还是双向？**已定 v1 双向**（`{0,2}` + `{2,2}`，两边进入都能双发，不会因 Facing 过期静默吞流）。单向版（顶进底出）必须把 0 放在入口通道并配合 Facing 同步，留到有明确单向需求时再做。
2. 是否保留 `CrossDevice`（四通实际三路分流）？本文暂不做。
3. `EmitterAttachment` 与 `device` 的跨附件依赖是否接受？本文接受：不做 Attach 期校验，未知 key 由 `DeviceAttachment` 记录；替代方案是把激活接线放进泛用 `DeviceAttachment`（需要 `IFlowEmitter` 能力接口，v1 不引入）。
4. `ItemSetup` 不参与状态同步/定义校验（用户要求保持中转职责）；状态同步只在 `FlowRuntime.Adopt`。若实机发现 `modifiedState` 仍为旧值，再单独评估修复点，不塞回 ItemSetup。
5. `ItemSetup` 的反射自动发现与文档裁决冲突：本次不动，另开清理任务；不要因为它在用反射就给装置目录也开反射先例。
6. `item.onLoaded`：Assembly-CSharp 已确认存在该符号，但签名/调用约定未实机验证；v1 不用，保留为“读档后立即 adopt”的优化项。若启用，必须整体 try/catch，不能中断读档。
7. 每实例可改配置（如玩家改 Split 通道）是否需要？v1 不做；将来用 `FLUXFORGE_DEVICE_CONFIG`（JSON string）+ `FLUXFORGE_DEVICE_SCHEMA`（int）两个 tag，按 §4.4 的规则迁移。

---

## 9. 评审记录

- **架构子代理**：确认目录放 Logic；泛用 attachment 只做创建/登记/订阅；反对反射；提出 `IFlowConfigurable`/`IFlowEmitter` 扩展点，本文按“v1 不引入”裁剪。
- **Split 子代理（两轮）**：确认基类 + 无参 sealed 变体；给出 Turn/TJunction/Cross/DoubleEmit 的通道与 emitCounts；第二轮纠正了常量基准（T 枝朝上/朝下、Double 的 0 必须在入口通道）、补齐 Turn 双向行为、指出同口双发重合与单向吞流风险，并补了穿过/全零吸收/混合计数/Turn `total==1` 等测试。本文采纳：T 用“枝朝上”基准、Double 改双向、测试清单扩充。
- **对抗审查子代理（两轮）**：第一轮指出 Attach 读 tag 会抹存档、Emitter 拆附件违反装配件规约 6、反射/文档漂移、共享实例、幽灵装置等风险；第二轮接受“Split 基类 + 常量变体”的反驳，但要求 adopt 前 sync、Split 物品必须声明 `FLOW_DEVICE`、Emitter 不要 Attach 期假阳性校验、`DeviceRegistry` 退订次序与 notifier 全量状态契约。本文逐条落到 §3、§4、§7。
- **数据/持久化子代理**：结论是 items.json 管目录定义、tag 管存档实例；验证了读档时序（工厂 → `TagSystem.Clone` → `item.onLoaded`），建议 `FLUXFORGE_` 前缀 + 一个 JSON string 配置 tag + schema tag，否决 `Fluxforge.x1` 点号命名。本文采纳其 tag 命名与“Attach 不读 tag”的结论；v1 裁掉 `device` 对象/config tag/`onLoaded`，保留为未来扩展（§4.4、§8）。
- **保留的反对意见**：对抗审查认为“若本轮不新增 ≥2 个分流变体，收益为负”；本文以“用户明确要 TJunction + DoubleEmit 两个分流变体”为前提。若最终只做绑定、不做 Split 物品，则只落泛用 attachment + adopt 泛化（减半版）。
- **实施结果（2026-11-01）**：Logic 目录重组为 `Interfaces / Routing / Reaction / Source`；Mod 层只保留 `DeviceAttachment`；`CollectorAttachment`/`IceBlockAttachment` 删除；`FlowRuntime` adopt 泛化；`ItemSetup` 保持原样；测试 230 / 230，构建 0 / 0。已复制 DLL 到游戏 `Mods\FluxForge` 做功能测试，尚未发布。

---

## 10. 结论

推荐按 §3 实施：**Logic 目录 + 一个 `device` attachment + 无参装置 + 泛化 adopt + Split 基类（TJunction / DoubleEmit，Turn 视需要保留）**。这样新增装置的成本是：

```text
Logic 类（实现 IFlowDevice/IFlowRouter，可选 IFlowNotifier）
+ FlowDeviceCatalog 一行
+ items.json 的 device 字段（及 attachments 中的 "device"）
```

Mod 层不再出现装置专属 attachment；tag 同步由 `IFlowNotifier` + `FlowStock` 统一完成；读档状态由首次发射的 adopt 统一回灌（adopt 内先 `SyncModifiedState`）。若只想先做绑定、暂不做 Split 物品，按“减半版”只落 `FlowDeviceCatalog` + `DeviceAttachment` + 泛化 adopt，Split 基类延后。
