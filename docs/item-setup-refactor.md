# 物品装配层重构（ItemAttachment）设计 · 定稿

> 状态：已按三份独立评审（正确性 / 一致性 / 红队）修订，可以实施
> 基线：`d92ef03`（构建 0 warning / 0 error，测试 162 全绿）
> 评审记录：见 `.superpowers/sdd/item-attachments/progress.md`

## 1. 背景

当前"物品行为挂接"靠 `FlowItemSetup.Attach` 里的 `switch (def.key)`。后果：

- 新增装配件必须改这个 `switch`（改现有文件）；
- `def.key` 同时承担"存档标识"与"代码分派键"两个职责。

## 2. 目标与非目标

### 目标

1. 新增**无参**装配件 = 只新建一个类，不改任何现有文件；
2. 一件物品可声明**多个**装配件，按声明顺序执行（同一独占资源至多一个，见 §4 规约 5）；
3. `def.key` 退回唯一职责：存档标识 / 贴图约定。

### 非目标

- 不改 `ItemFactory` 的数据装配五步；
- 不改任何装置运行时（`FlowRuntime` / `FlowDriver` / `DeviceRegistry` / `LiquidStock` / 视觉层）；
- 不改 UI 结构；
- 不改存档格式；
- **不引入装配件参数通道**（`grid` 这类配置仍由 `def` 顶层字段提供，见 §4 规约 6）；
- 不做热插拔、不扫描程序集外部。

## 3. 目标结构

```
src/Mod/Items/
├─ Setup/
│  ├─ ItemFactory.cs                  数据装配（仅位移，逻辑不变）
│  ├─ ItemAttachment.cs               行为契约（新增）
│  ├─ ItemSetup.cs                    入口 + 表 + 分派（原 FlowItemSetup 改名）
│  ├─ EmitterActivationCallbacks.cs   原生委托桥（不是装配件，见 §4 规约 7）
│  └─ Attachments/
│     ├─ MachineAttachment.cs
│     ├─ EmitterAttachment.cs
│     ├─ CollectorAttachment.cs
│     └─ IceBlockAttachment.cs
├─ FlowItemRegistry.cs             配置读取（不进 Setup）
├─ ItemTypeLocalization.cs
├─ StatusLine.cs
└─ Tooltip/
```

命名空间不变（`ProbablyBagMod.Mod`），移动文件不需要改 `using`。

**成员归属**（评审指出旧设计未定义，实施时必须照此）：

| 成员 | 去处 |
|---|---|
| `ParseGrid` | `MachineAttachment` private static |
| `GetEmitterInventory` / `MayActivateEmitter` / `CanActivateEmitter` / `ActivateEmitter` | `EmitterAttachment` **internal static**（`EmitterActivationCallbacks` 调用它们） |
| `EmitterActivationCallbacks` | `Setup/EmitterActivationCallbacks.cs`（**不放 Attachments/**） |
| `Subscribe(IFlowNotifier)` | 移到 `FlowDeviceEvents.Subscribe`，Collector / IceBlock 共用 |
| `FlowDeviceType` / `FlowDeviceDisplayName` | 留在 `ItemSetup`（`PBModEntry` 引用） |
| `FlowMachineKey` / `EmitterKey` / `CollectorKey` / `IceBlockKey` | **删除**（改由各装配件的 `Name` 承担） |

## 4. 基类契约

```csharp
public abstract class ItemAttachment
{
    /// 装配件名，items.json 用它引用。子类硬编码，全局唯一。
    public abstract string Name { get; }

    /// 独占资源标识：占用同一槽位的装配件，一件物品最多一个。
    /// 目前用到："contentWindow"（面板）、"device"（DeviceRegistry）。
    /// 不占独占资源返回 null。
    public virtual string ExclusiveSlot => null;

    /// 只挂行为。
    public abstract void Attach(Il2Cpp.GameItem item, FlowItemDefinition def);
}
```

### 硬性规约

1. **无实例字段**。实例启动时创建、全局共享。
2. **无静态可变状态**。禁止 `static` 缓存 `GameItem` / `IntPtr` / `PixelWindow`；读档后指针会复用。
   （启动时会用反射检查实例字段数，非 0 打 Warning。）
3. 顶层 `public sealed class`，**public 无参构造**，不嵌套。
4. **不得覆盖 `ItemFactory` 五步写入的内容**：identifier / 贴图 / shape / 名称 / 说明 / 风味 / itemTypes / value / 配置声明的 tags。
   允许调用**游戏原生装配 API**（如 `WaterHelper.InitLiquidContainerItem`）建立行为所需状态——它会写容器 tag 和 `WATER` 类型，这是行为的一部分，不算越界。
5. **独占资源**：`ExclusiveSlot` 相同的两个装配件不能挂在同一件物品上（运行期检查，冲突打 Error 并跳过后者）。
6. **装配件不得互相依赖**；需要依赖就该合并。`def` 是唯一配置入口。
7. 装配件必须**无参**。"需要参数"= 该参数应成为 `def` 的顶层字段，或该装配件应被拆分。

## 5. 发现与初始化

`ItemSetup.Initialize()`（幂等，`_initAttempted` 在**方法第一行**置位，失败不重试）：

```
try
{
    types = typeof(ItemAttachment).Assembly.GetTypes()
}
catch (ReflectionTypeLoadException e)
{
    types = e.Types 过滤 null
    Error：记录 e.LoaderExceptions（去重）——类型加载失败会被误判成 JSON 拼错
}

candidates = types 中 typeof(ItemAttachment).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
按 t.FullName 的 Ordinal 升序排序          // 保证"保留第一个"是确定行为

foreach t in candidates:
    try   { instance = (ItemAttachment)Activator.CreateInstance(t); }
    catch { Error(t.FullName + 异常)；continue; }     // MissingMethodException 也走这里
    name = instance.Name（getter 抛异常也 catch）
    name 为空        → Error(t.FullName)，跳过
    name 已存在      → Error("「" + name + "」重复：" + 已有类型 + " / " + 新类型)，保留先者，跳过
    name 规整        → Trim()
    入字典
    instance 有实例字段 → Warning（违反规约 1）

if (count == 0) Error("发现 0 个装配件：过滤条件或程序集有误")
Msg("[装配] 已发现 N 个：" + name + "(" + 类型简称 + ")" ...)
```

整个方法体包在 `try/catch` 里：任何未预期异常只记 Error，表保持为空，不向外抛。

调用点：`PBModEntry.OnInitializeMelon` 的**现有步骤之后**（PatchRunner / ModHook / ItemTypeLocalization 之后），一行。

**兜底**：`ApplyAttachments` 发现 `_initAttempted == false` 时调用一次 `Initialize()` 并打 Warning
`[装配] 装配件表未在启动期初始化，已在物品创建时补建`。
`_initAttempted` 为 true 但表为空时**不再重试**（避免每次物品创建重跑反射 + 刷日志）。

## 6. 执行

```csharp
private static void ApplyAttachments(Il2Cpp.GameItem item, FlowItemDefinition def)
{
    try                                          // ① 整体边界，异常不得冒到原生栈
    {
        if (!_initAttempted) { Warning(...); Initialize(); }

        string[] names = def.attachments;
        if (names == null || names.Length == 0) return;    // ② 合法，静默

        HashSet<string> usedSlots = null;                   // 懒分配
        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i]?.Trim();
            if (string.IsNullOrEmpty(name)) continue;

            ItemAttachment a = Get(name);                   // 字典 OrdinalIgnoreCase
            if (a == null)
            {
                MelonLogger.Msg("[装配]「" + def.key + "」引用了未识别的装配件「" + name + "」");
                continue;
            }

            string slot = a.ExclusiveSlot;
            if (slot != null)
            {
                usedSlots = usedSlots ?? new HashSet<string>(StringComparer.Ordinal);
                if (!usedSlots.Add(slot))
                {
                    MelonLogger.Error("[装配]「" + def.key + "」独占槽「" + slot + "」已被占用，跳过「" + name + "」");
                    continue;
                }
            }

            try { a.Attach(item, def); }
            catch (Exception e)
            {
                MelonLogger.Error("[装配]「" + def.key + "」" + name + " 失败：" + e);
            }
        }
    }
    catch (Exception e)
    {
        MelonLogger.Error("[装配]「" + def.key + "」装配流程异常：" + e);
    }
}
```

要点：

- **保留 `Create` 原有的两层 try/catch**（数据装配失败返回 null；装配失败返回半成品物品）；
- 逐装配件 try/catch 只隔离单个装配件，**不能替代** ① 的外层边界；
- 未知名字用 `Msg`（作者决定：配置笔误不需要 Error 级），物品照常产出；
- 同名只对**同一槽位**报错；不同槽位（machine + emitter）正常共存。

## 7. 配置

```json
{ "key": "flow_machine", "attachments": ["machine"], ... }
```

| 规则 | 说明 |
|---|---|
| **必须是数组** | 写成 `"attachments": "machine"` 会让整条反序列化失败 → `FlowItemRegistry` 跳过该项 → **物品直接不存在** |
| 名字比较 | 读取时 `Trim()`，字典用 `OrdinalIgnoreCase` |
| 字段缺失（null） | 不进入循环，**不输出任何日志**（模板物品是合法产物） |
| 空数组 `[]` | 同上 |
| 名字未识别 | 一条 `Msg`，继续 |
| 同一槽位冲突 | `Error`，跳过后者 |

命名规约：行为单数名词、不带 `flow_` 前缀、与物品 key 无关（`machine` / `emitter` / `collector` / `ice_block`）。

迁移：

```
flow_machine   → ["machine"]
flow_emitter   → ["emitter"]
flow_collector → ["collector"]
ice_block      → ["ice_block"]
```

## 8. 实施与提交边界

**一个提交完成全部改动。**理由（评审结论）：拆出"新分派已上线、JSON 还没加字段"的中间态，会让四件物品同时失去全部行为且零日志；而"只加新类不接线"的中间态又只能复制代码，无法用"纯位移"校验。

改动清单（一个提交）：

1. 新增 `ItemAttachment` + 四个装配件 + `EmitterActivationCallbacks`；
2. `ItemFactory.cs` 移入 `Setup/`（内容不变）；
3. `FlowItemSetup.cs` → `Setup/ItemSetup.cs`，删除 switch 与四个旧方法，加表 / `Initialize` / `ApplyAttachments`；
4. `FlowDeviceEvents` 增加 `Subscribe`；
5. `PBModEntry`：3 处改名引用（`:36` / `:67` / `:136`）+ 1 行 `Initialize()`；
6. `FlowItemRegistry`：`FlowItemDefinition` 增加 `string[] attachments`；
7. `FlowTags`：注释里的 `<see cref="FlowItemSetup"/>` 改为 `<see cref="ItemSetup"/>`；
8. `items.json` 四条加 `attachments`。

## 9. 验证

| 项 | 命令 | 通过标准 |
|---|---|---|
| 构建 | `dotnet build ProbablyBagMod.csproj -c Release` | 0 warning / 0 error |
| 单测 | `dotnet test tests\ProbablyBagMod.Tests\...\csproj -c Release` | 162 全绿 |
| 结构 | `git diff --stat` | 只涉及 §8 清单内的文件 |
| 引用 | grep `FlowItemSetup` / `FlowMachineKey` | 0 命中 |

## 10. 风险与对策

| 风险 | 对策 |
|---|---|
| `GetTypes()` 抛异常 | 整个 `Initialize` 包 try/catch；`LoaderExceptions` 入日志 |
| 扫描到 0 个 | Error（不是 Msg） |
| 类型被静默跳过 | 逐个 `Activator` + catch + Error 类型全名 |
| 重复 `Name` 选错实现 | 按 `FullName` 排序，日志同时打名字与类型 |
| 共享实例带状态 | 规约 1/2 + 启动期实例字段 Warning |
| 独占槽冲突 | `ExclusiveSlot` + 运行期 Error |
| 初始化被改早 | 调用点固定在 `OnInitializeMelon` 现有步骤之后 |
| 回滚方向搞反 | **只回滚代码是安全的**（旧 switch 仍工作，新 JSON 字段被忽略）；**只回滚 JSON 是危险的**（新代码看到 null → 全部纯数据）。代码与 JSON 同向回滚 |

## 11. 实机待验收（本轮不做）

1. 启动日志：`[装配] 已发现 4 个：collector(CollectorAttachment), emitter(EmitterAttachment), ice_block(IceBlockAttachment), machine(MachineAttachment)`（按名字 Ordinal 排序）；
2. 流量机双击开窗，面板里有 4×3 网格；
3. 发射装置可被水瓶灌水，菜单"激活"能发射，水量下降；
4. 收集装置 tooltip 存量随吸收更新；
5. 冰块被流源撞后融化，耗尽后从网格移除；
6. 组合验证（临时改一条配置）：`flow_machine` 加 `"emitter"` → 窗口与灌水/激活同时存在。

## 12. 评审结论裁决

| 问题 | 结论 |
|---|---|
| Q1 抽象类 vs 接口 | **保留抽象类**（作者决定）。评审建议改接口（仓库 0 抽象类、6 接口），理由记录在案；`ExclusiveSlot` 需要默认实现，抽象类更合适 |
| Q2 字段名 | `attachments`（复数，对齐 `itemTypes`/`tags`/`statusLines`） |
| Q3 逐物品提示 | 不逐物品提示；扫描 0 个 → 启动 Error 一次 |
| Q4 兼容层 | 不需要（内部类型，JSON 内嵌同一 DLL） |
