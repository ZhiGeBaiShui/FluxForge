# 物品装配层重构（ItemAttachment）设计 · v3

> 状态：**历史设计记录**（v3，基线 `d92ef03`，测试 162）。本文保留为决策史，**不代表当前实现**。
>
> **当前实现差异（2026-11-04 核对）**：
>
> - 目录：`src/Mod/Items/` → `src/Mod/ItemSpawn/`，`Setup/` → `Services/`；`ItemFactory.cs` 在 `ItemSpawn/Services/`。
> - **反射自动发现回来了**：`FFModEntry.Init` 调 `ItemSetup.Initialize()`，用 `Assembly.GetTypes()` + `Activator.CreateInstance` 建表；本文 §5「放弃反射」的结论已被后续实现推翻。名字重复会让 `Dictionary.Add` 在启动期抛异常。
> - `ItemSetup` 的职责改为「空白物品 → attachments → `ItemFactory.Apply(item, def)`」；`ItemFactory` 只装配已有物品，不再 `Create`。
> - 当前装配件共 4 个：`device` / `flow_container` / `water_bottle` / `emitter`；本文提到的 `machine` / `collector` / `ice_block` 三个专属装配件已删除，装置统一走 `device`（只写 tag）。
> - `FlowItemDefinition` 新增 `device` / `anchorX` / `anchorY`；`device` 同时是持久化 tag 名。
> - 测试基线：当前 **221 / 221**（本文 §9 的 162 是当时基线）。
> - §3 的目录树、§5 的注册表、§7 的 items.json 示例、§11 的实机验收都属于当时状态；实际以 `src/Mod/ItemSpawn/` 与 `assets/items.json` 为准。

> 评审与裁决记录：`.superpowers/sdd/item-attachments/progress.md`

## 1. 背景

当前"物品行为挂接"靠 `FlowItemSetup.Attach` 里的 `switch (def.key)`。后果：

- 新增装配件必须改这个 `switch`（改现有文件）；
- `def.key` 同时承担"存档标识"与"代码分派键"两个职责。

## 2. 目标与非目标

### 目标

1. 新增**无参**装配件 = 新建一个类 + 在注册表加一行，不改任何逻辑；
2. 一件物品可声明**多个**装配件，按声明顺序执行（组合是否互斥由作者决定，见 §4 规约 5）；
3. `def.key` 退回唯一职责：存档标识 / 贴图约定。

### 非目标

- 不改 `ItemFactory` 的数据装配五步；
- 不改任何装置运行时（`FlowRuntime` / `FlowDriver` / `DeviceRegistry` / `FlowStock` / 视觉层）；
- 不改 UI 结构；
- 不改存档格式；
- **不引入装配件参数通道**（`grid` 这类配置仍由 `def` 顶层字段提供）；
- **不做反射自动发现**（见 §5 的取舍记录）。

## 3. 目标结构

```
src/Mod/Items/
├─ Setup/
│  ├─ ItemFactory.cs                  数据装配（仅位移，逻辑不变）
│  ├─ ItemAttachment.cs               行为契约
│  ├─ ItemSetup.cs                    入口 + 注册表 + 分派（142 行）
│  ├─ EmitterActivationCallbacks.cs   原生委托桥（不是装配件）
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

命名空间不变（`FluxForge.Mod`）。

**成员归属**：

| 成员 | 去处 |
|---|---|
| `ParseGrid` | `MachineAttachment` private static |
| `GetEmitterInventory` / `MayActivateEmitter` / `CanActivateEmitter` / `ActivateEmitter` | `EmitterAttachment` internal static |
| `EmitterActivationCallbacks` | `Setup/EmitterActivationCallbacks.cs`（不放 Attachments/） |
| `Subscribe(IFlowNotifier)` | `FlowDeviceEvents.Subscribe` |
| `FlowDeviceType` / `FlowDeviceDisplayName` | `ItemSetup` |
| `FlowMachineKey` / `EmitterKey` / `CollectorKey` / `IceBlockKey` | 删除 |

## 4. 基类契约

```csharp
public abstract class ItemAttachment
{
    /// 装配件名，items.json 用它引用。子类硬编码，全局唯一。
    public abstract string Name { get; }

    /// 只挂行为。
    public abstract void Attach(Il2Cpp.GameItem item, FlowItemDefinition def);
}
```

### 硬性规约

1. **无实例字段**。实例启动时创建、全局共享。
2. **无静态可变状态**。禁止缓存 `GameItem` / `IntPtr` / `PixelWindow`；读档后指针会复用。
3. 顶层 `public sealed class`，public 无参构造，不嵌套。
4. **不得覆盖 `ItemFactory` 五步写入的内容**（identifier / 贴图 / shape / 文本 / itemTypes / value / 配置声明的 tags）。允许调用游戏原生装配 API（如 `WaterHelper.InitLiquidContainerItem`）建立行为所需状态。
5. **组合由作者负责**：一件物品挂哪些装配件、是否互斥（两个面板 / 两个装置 / 两组激活委托），由作者在 `items.json` 里决定并保证。运行期**不做**检查；同一字段被写两次时后者生效。
6. **装配件不得互相依赖**；需要依赖就该合并。`def` 是唯一配置入口。
7. 装配件必须**无参**。需要参数 = 该参数应成为 `def` 的顶层字段，或该装配件应被拆分。

## 5. 装配件注册表

```csharp
private static readonly ItemAttachment[] All =
{
    new MachineAttachment(),
    new EmitterAttachment(),
    new CollectorAttachment(),
    new IceBlockAttachment(),
};

private static readonly Dictionary<string, ItemAttachment> _attachments = BuildTable();
```

- **唯一注册点**：新增装配件 = 新建一个类 + 在 `All` 里加一行；
- 静态初始化，第一次访问 `ItemSetup` 时完成（4 个纯托管对象 + 一次建表，无反射、无 IO）；
- 名字比较 `OrdinalIgnoreCase`；同名时后者覆盖前者（数组里一眼可见）；
- **不对"类是否写对"做任何运行期校验**。

### 取舍记录：为什么放弃反射自动发现

v2 曾实现反射扫描（`Assembly.GetTypes()` + 逐个 `Activator.CreateInstance`），并为 5 类失败加了诊断，合计约 160 行（`Initialize` 106 行 + 4 个辅助函数）。v3 删除。

理由：那 5 类失败**全部是反射自己引入的**——

| 反射下的"失败" | 手写数组时 |
|---|---|
| 类没有 public 无参构造 | 编译错误 |
| 类名/命名空间写错 | 编译错误 |
| 类型加载失败（缺依赖） | 不会发生（直接引用类型） |
| 扫出 0 个 | 不可能（数组写死） |
| 重复 `Name` 赢家不确定 | 两行并排，一眼可见 |

用 160 行把编译期问题重新变回"运行期可观测"，是净损失。**运行期只防御真正的外部输入**——`items.json` 里的字符串。

## 6. 执行

```csharp
private static void ApplyAttachments(Il2Cpp.GameItem item, FlowItemDefinition def)
{
    if (def.attachments == null || def.attachments.Length == 0) return;   // 合法，静默

    HashSet<string> usedSlots = null;
    for (...)
    {
        name = names[i]?.Trim();             // 外部输入：trim + 忽略大小写
        attachment = Get(name);
        if (attachment == null) { Msg(未识别 + 已注册清单); continue; }

        独占槽冲突 → 不检查（作者责任，见 §4 规约 5）

        try { attachment.Attach(item, def); }
        catch { Error(def.key + name)；物品照常返回 }
    }
}
```

要点：

- **异常边界只有一个**：`Create` 里包 `ApplyAttachments` 的那一层（原生回调的唯一出口）。方法内部**不再**重复包同粒度的 try；
- 逐装配件 try/catch 的作用是**隔离**，不是替代外层：一个装配件失败不该让其余装配件也被跳过；
- 未知名字用 `Msg`，并附"已注册：..."，物品照常产出；

## 7. 配置

```json
{ "key": "flow_machine", "attachments": ["machine"], ... }
```

| 规则 | 说明 |
|---|---|
| **必须是数组** | 写成 `"attachments": "machine"` 会让整条反序列化失败 → 该物品**直接不存在** |
| 名字比较 | 读取时 `Trim()`，字典 `OrdinalIgnoreCase` |
| 字段缺失（null）/ 空数组 | 都不进入循环，**不输出日志**（模板物品是合法产物） |
| 名字未识别 | 一条 `Msg`（附已注册清单），继续 |

命名规约：行为单数名词、不带 `flow_` 前缀、与物品 key 无关：`machine` / `emitter` / `collector` / `ice_block`。

现有四条：`flow_machine → ["machine"]`、`flow_emitter → ["emitter"]`、`flow_collector → ["collector"]`、`ice_block → ["ice_block"]`。

## 8. 改动清单（已提交，`4ba9fe6` + 本次简化）

1. 新增 `ItemAttachment` + 四个装配件 + `EmitterActivationCallbacks`；
2. `ItemFactory.cs` 移入 `Setup/`（100% rename）；
3. `FlowItemSetup.cs` → `Setup/ItemSetup.cs`：删 switch / 四个旧方法 / 四个 key 常量，加注册表 + `ApplyAttachments`；
4. `FlowDeviceEvents` 增加 `Subscribe`；
5. `FFModEntry`：3 处改名引用；**无初始化调用**；
6. `FlowItemRegistry`：`FlowItemDefinition` 增加 `string[] attachments`；
7. `FlowTags`：注释 cref 改为 `ItemSetup`；
8. `items.json` 四条加 `attachments`。

## 9. 验证

| 项 | 命令 | 通过标准 |
|---|---|---|
| 构建 | `dotnet build FluxForge.csproj -c Release` | 0 warning / 0 error |
| 单测 | `dotnet test tests\FluxForge.Tests\...\csproj -c Release` | 162 全绿 |
| 引用 | grep `FlowItemSetup` / 四个 key 常量 | 0 命中 |

## 10. 风险与对策

| 风险 | 对策 |
|---|---|
| 共享实例带状态 | 契约 §4 规约 1/2，评审把关 |
| 配置写错（名字/字段名/裸字符串） | 未识别名字时提示并列出已注册清单；`必须数组` 写进 §7 |
| 回滚方向 | **只回滚代码安全**（旧 switch 仍工作，新字段被忽略）；**只回滚 JSON 危险**（新代码看到 null → 全纯数据）。同向回滚 |

## 11. 实机待验收（未执行）

1. 流量机双击开窗，面板里有 4×3 网格；
2. 发射装置可被水瓶灌水，菜单"激活"能发射，水量下降；
3. 收集装置 tooltip 存量随吸收更新；
4. 冰块被流源撞后融化，耗尽后从网格移除；
5. 组合验证：`flow_machine` 临时加 `"emitter"` → 窗口与灌水/激活同时存在。

## 12. 评审结论裁决

| 问题 | 结论 |
|---|---|
| 抽象类 vs 接口 | **抽象类**（作者决定；`ExclusiveSlot` 需要默认实现） |
| 字段名 | `attachments`（复数，对齐 `itemTypes`/`tags`/`statusLines`） |
| 未知名字日志级别 | `Msg`（作者要求"调试信息"），并附已注册清单 |
| 兼容层 | 不需要（内部类型，JSON 内嵌同一 DLL） |
| 自动发现 vs 手写注册表 | **手写注册表**（v3 改判：反射引入的失败模式全部是自造的，见 §5） |
| 独占槽运行期检查 | **删除**（v3 改判：装配件组合是作者写配置时的决定，不是运行时外部输入；而且原方案只覆盖 2 个字段、漏了三个激活委托字段，属于不完整的保护） |
