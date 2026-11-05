# FluxForge 待评审改进项（状态版）

- 基线：`466717f` + 当前未提交工作区（装置状态适配器单例化与 Create/Load 迁移、Logic 事件接口替换、本文；行为以代码为准）
- 状态：**P0-1 已实施（有遗留）**、**P1-1 接受现状**、**P1-2 已实施**、**P2-1 backlog**、**P2-2 延后/待调研**、**P3-1 历史记录**、**P4-1 装置状态架构已实施（适配器单例化）**
- 验证（2026-11-05，本轮）：
  - `dotnet build FluxForge.csproj -c Release --no-incremental -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"` → 0 warning / 0 error
  - `dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release` → 290 / 290 通过，0 失败 0 跳过
- 本轮变化：`IFlowState` / `FlowTagMap` / `FlowStock.ReadInto` 路径已删除；
  装置状态改走无状态 `DeviceStateAdapter` 单例（`Create` / `Load` / `Save`）+ 静态 `GameItemTags`，
  过渡期的 `ITagStore` / `GameItemTagStore` 封装也已删除，详见 P4-1 与 `docs/device-state-architecture.md`。

| 编号 | 项目 | 结论 | 状态 |
|---|---|---|---|
| P0-1 | 网格运行环境生命周期 | 构造期注册已落地；不变量报错 / 兜底移除未做 | **部分实施** |
| P1-1 | Patch 全 mod 失败 | 接受「整 mod 禁用」，不引入 Guarded Patch | **不实施** |
| P1-2 | `SaveExited` 清理范围 | `EscapeUIManager.OnMainMenu` + `GameMaster.QuitToMenu` → `Shutdown`；另加 `EmporiumDestroyed` 场景兜底 | **已实施，实机修正** |
| P2-1 | 每帧分配 | 真实但低影响；`FlowStock.Total` 已不再分配整份属性 | **backlog（部分收窄）** |
| P2-2 | `DeviceRegistry` 销毁清理 | `ItemDestroyed` 方案未采用；清理仍手动 | **延后** |
| P3-1 | 文档同步 | README / Mod README / Lifecycle README / 设计稿状态头 | **历史记录** |
| P4-1 | 装置状态适配器 / tag 映射 | 无状态 `DeviceStateAdapter` 单例（Create/Load/Save）+ `GameItemTags` + `FlowTagNaming`；删除 `ITagStore` / `FlowTagMap` / `IFlowState` 旧同步路径 | **已实施** |

---

# P0-1：网格运行环境生命周期

## 已实施

`GridCreatedPatch` 现在是**一个** `[HarmonyPatch(typeof(GameGridInventory))]` 类，用 `TargetMethods()` 同时覆盖：

- `typeof(GameGridInventory).GetConstructors()` 的全部公开构造函数；
- 全部公开 `SetShape` 重载。

所有目标共用同一个 `Postfix`：

```csharp
public static void Postfix(GameGridInventory __instance)
{
    GridRegistry.Register(__instance);   // 幂等
}
```

约束与理由：

- **不再把 `Type` 传给反射 helper**。直接写 `typeof(GameGridInventory)`；游戏 `ModScanner` 会把「对计算出的 `Type` 反射」判为 `reflection-external`，而 `manifest.xml` 的 `<Capabilities />` 保持为空。
- **私有无参构造不注册**：此时 `inventoryShape` 还是 `null`。
- **不变量目标**：`FlowRuntime` 的生命周期 == `GameGridInventory` 的生命周期。
- `Register` 已幂等，构造函数 / `SetShape` 重复命中不会重复创建 runtime。

## 遗留（与初版建议的差异）

初版 P0 还有两条没有实施：

1. **`GridRegistry.Emit` 仍未做不变量报错**：

   ```csharp
   public static bool Emit(GameItem item)
   {
       GameGridInventory parent = (GameGridInventory)item.parentInventory;
       return Get(parent).Emit(item);   // runtime 为 null 时 NRE
   }
   ```

   正常路径由构造期注册保证；异常路径仍是 NRE，不是带上下文的 `InvalidOperationException` + Error 日志。

2. **`ItemAttached` 仍保留兜底注册**：

   ```csharp
   FlowRuntime runtime = GridRegistry.Get(inventory);
   if (runtime == null)
   {
       runtime = GridRegistry.Register(inventory);
   }
   ```

   代码注释说明它覆盖「登记被整体清空（例如退出存档）而网格还没销毁」；正常路径下网格在公开构造期已注册。**是否收紧为日志/异常，需要先确认没有「Shutdown 之后旧网格仍存活并继续接物品」的合法路径**，当前未验证。

## 为什么不再使用懒注册作为主路径

```text
网格对象已存在
FlowRuntime 不存在
```

懒注册会把生命周期缺陷藏到第一次发射；构造期注册保证「网格出生即有运行环境」，发射路径只查询。

## 验收（更新）

1. 新建游戏，玩家网格只放一个 `flow_emitter`；加水、激活 → 生成流源，无 NRE。
2. 启动时每个 `GameGridInventory` 都有对应 `FlowRuntime`。
3. 同一网格再放收集器，重复激活；不重复注册、不重复视觉层。
4. 销毁网格再创建，发射器仍可用。
5. 读档后重复上述路径。
6. 网格运行时 `SetShape` 扩容后仍可发射。

## 风险

- 所有网格在构造期创建 `FlowRuntime`（`FlowVisualLayer` 也在构造时装配），对象数量上升；需要测启动内存与多网格场景。
- 构造函数 Postfix 运行时 `rectTransform` / `inventoryShape` 必须已可用；需实机验证。
- `ItemAttached` 兜底不删除，就无法用异常暴露「构造期漏注册」。

---

# P1-1：Patch 全 mod 失败策略（接受，不实施）

结论不变：**不做 Guarded Patch、不做手动 patch 计划、不按 patch 单独 try/catch**。

官方 `ModLoader` 的顺序：

```text
PatchAll(assembly)
  → 任一 patch 失败 → 捕获 → UnpatchAll → mod 本次会话禁用
```

这是有意的「失败必须响亮」：游戏更新拿掉 patch 目标时，玩家看到的是 mod 加载失败，而不是功能静默缺失。

需要更新的只有一点：**不要再引用旧的 patch 数量（9）**。当前 `GridCreatedPatch` 的目标数 = `GameGridInventory` 的公开构造函数数 + 公开 `SetShape` 重载数，其余 patch（`ItemAttached`、`ItemDetached`、`ItemPickedUp`、`InventorySorted`、`SaveExited` ×2、`EmporiumDestroyed`、`FlowTooltipPatch`、`LoadFromAtlasPatch`）各 1 个。`Player.log` 里的数字随游戏版本和 patch 目标变化，不是契约。

验收：游戏更新后若 patch 目标缺失，日志应能直接看到 `Init failed` / `LoadError`；发布说明写明「游戏更新后需重新验证 patch 目标」。

---

# P1-2：`SaveExited` 清理范围（已实施，实机修正）

## 根因

最初把普通 Escape 目标挂成 `PlayerStore.Reset`。实机日志证明这不安全：

```text
PlayerStore..ctor → Reset()
  → SaveExited.Postfix → FlowDriver.Shutdown()
  → GridRegistry.Clear()

EmporiumEntry.Start → AddAlwayItems
  → DirectoryMaster.Item → PlayerStore.Instance
  → new PlayerStore() → Reset()
  → Clear count=13
```

`PlayerStore.Reset` 同时被：

```text
PlayerStore 构造函数
PlayerStore.PreLoadStore（ES3 读档前创建 PlayerStore）
EscapeUIManager.MainMenuWithAnalytics（真正退出）
```

所以它会在 `EmporiumEntry.Start` 构造展示窗 / 柜台网格的过程中清空 `GridRegistry`：

```text
Register 18x4 / 7x5 / 24x10
  → PlayerStore..ctor → Reset → Clear
  → 展示窗 / 柜台 runtime 消失
```

这解释了实测现象：

```text
库房：读档后新建 → 正常
展示窗 / 柜台：Start 早期创建 → 被清 → 发射 runtime=False
扩容展示栏：SetShape 重新注册 → 正常
```

## 修复

`SaveExited.TargetMethods()`：

```csharp
MethodBase mainMenu = AccessTools.Method(typeof(EscapeUIManager), "OnMainMenu");
MethodBase quit     = AccessTools.Method(typeof(GameMaster), "QuitToMenu");
```

Postfix：

```csharp
FlowDriver.Shutdown();
```

覆盖两条真实退出路径：

```text
普通 Escape：
  EscapeUIManager.OnMainMenu
  → SaveExited.Postfix → Shutdown

团灭 / 调试：
  GameMaster.QuitToMenu
  → SaveExited.Postfix → Shutdown
```

**不能用 `PlayerStore.Reset`**：构造函数 / 预读档会误触发（本 bug 的根因）。

**不能用 `GameMaster.CloseAllScenes`**：它在「开始游戏」时也会被调用（`NewGame` / `MenuScene` 按钮回调），用它做清理会在进入存档后立刻执行一次，把刚注册的网格全部清掉。

## 为什么必须是 `Shutdown` 而不是 `Reset`

| 对比项 | `Reset`（#8 场景卸载） | `Shutdown`（#5 退档 / #5b 兜底 / #9 卸载） |
|---|---|---|
| 流源 | 存活 runtime `KillAll` | 不清，直接释放视图 |
| 网格登记 | **保留** | **清空** |
| 装置登记 | `DeviceRegistry.Clear` | `DeviceRegistry.Clear` |
| 视觉视图 | 保留 | 全部 `Dispose` |

`Reset` 保留网格登记，不符合「退出存档清空全部 mod 引用」；场景卸载又不等于网格销毁，所以两者不能合并。

## 场景级兜底：`EmporiumDestroyed`

`src/Mod/GridBuilding/Lifecycle/EmporiumDestroyed.cs`：

```csharp
[HarmonyPatch(typeof(EmporiumEntry), "OnDestroy")]
internal static class EmporiumDestroyed
{
    private static void Postfix(EmporiumEntry __instance)
    {
        if (EmporiumEntry.Instance != __instance)
        {
            return;
        }

        FlowDriver.Shutdown();
    }
}
```

理由：

- 原版退档**不会**逐个调用 `GameItemElement.Destroy`；`EmporiumEntry.ResetData()` 只 `Object.Destroy(Instance)`，`GameItem` 是普通 C# 对象，靠失去引用后 GC。
- 正常 Escape 路径（`EscapeUIManager.MainMenuWithAnalytics`）是 `PlayerStore.Instance.Reset()` → `EmporiumEntry.ResetData()` → `PlayerStore.ResetData()` → `LoadScene`；`EmporiumEntry.OnDestroy` 会在 `SaveExited` 之后再次触发，`Shutdown` 幂等，重叠安全。
- `EmporiumEntry.Awake` 发现重复 `Instance` 时会立刻 `Destroy(this)`；守卫 `Instance != __instance` 防止把真正实例的注册表清掉。
- `FlowDriver.Shutdown` 可重复调用，与 #5 重叠是安全的。

## 验收

1. 正常 Escape → 主菜单 → Continue；展示窗、前台/后台柜台、库房发射器均可用。
2. 团灭/调试 `QuitToMenu` 后 Continue；发射器可用。
3. 读档过程中不再出现 `GridRegistry.Clear`；`PlayerStore` 构造后展示窗 / 柜台 runtime 仍在。
4. 退出存档后 `GridRegistry`、`DeviceRegistry` 为空，视觉层已 `Dispose`。
5. 下一次进入存档时，所有 `GameGridInventory` 构造期重新注册。
6. `sceneUnloaded` 路径仍只做 `Reset`，不误清仍存活的网格。
7. `EmporiumEntry` 被销毁的路径不残留视觉层；重复 `Shutdown` 不抛异常。

## 风险

- 还差一次完整实机 smoke：Escape → Continue，确认 #5 在真正退出时仍执行。
- `Shutdown` 发生在 `LoadScene` 之前；过渡期间已禁止交互，网格随后销毁，不应再访问 mod 视图。
- `EmporiumEntry.Instance` 的重复实例路径需要实机确认守卫行为。

## 影响文件

```text
src/Mod/Bootstrap/SaveExited.cs
src/Mod/GridBuilding/Lifecycle/GridCreatedPatch.cs
src/Mod/GridBuilding/Lifecycle/EmporiumDestroyed.cs
src/Mod/GridBuilding/Lifecycle/README.md
```

# P2-1：每帧分配（backlog）

真实分配，低影响，**不进本轮**：

1. `FlowManager.MoveFlowEntity` 每帧 `new FlowPos(...)`（class），可用 scratch 复用；需确认 `TryFindBlocker` 不保留引用。
2. `FlowVisualLayer.ComputeBodyColor` 仍对 `IReadOnlyList` 做 `foreach`，会产生枚举器，可改索引循环。
3. `FlowStock.Total` 已改为直接累加液体 tag，不再构造 `FlowProperties`；发射路径的 `FlowStock.Read` 仍会分配一份 37 槽对象，属于可接受的一次性成本。

验收：移动、分流、收集、发射库存判断、视觉颜色结果全部不变；用 Profiler 或 GC 计数对比。

风险：不能缓存 `SubstanceTotal` / `FlowProperties`（会破坏实时加权平均）；`FlowPos` scratch 不能被异步或延迟使用。

---

# P2-2：`DeviceRegistry` 销毁清理（延后）

## 决策

**`ItemDestroyed` patch 方案未采用**，不再作为待办实现项。

理由：

- 原版是否会按物品调用 `GameItemElement.Destroy` 未确认；`EmporiumDestroyed` 的注释已记录「退档不逐个 Destroy，靠 GC」。
- 在未确认生命周期前新增 patch，会把「物品被销毁」和「物品被拿起/移动/换容器」混在一起，风险高于收益。

## 现状（设计如此）

`DeviceRegistry` 保存：

```text
GameItem → DeviceHost
  ├─ Router
  ├─ Reactor
  └─ Bindings[]（Key / Adapter / Device）

装置实例 → DeviceHost / GameItem
```

物品离开网格（`ItemDetached` / `ItemPickedUp`）只清 `FlowGridAdapter` 的格表，**不注销装置**——装置跟着物品走，不能在离开网格时销毁。

清理只发生在：

```text
DeviceRegistry.Clear()
  ← FlowDriver.Reset（#8）/ FlowDriver.Shutdown（#5、#5b、#9）
或装置主动 Exhausted
```

影响是**有界保留**：每个会话放置过的装置数量，持有 `GameItem` 引用与订阅。没有观察到行为错误，但属于真实保留。

## 仍需调研（UNVERIFIED）

```text
1. 原版物品被销毁时，实际走哪个函数？
2. GameItemElement.Destroy 是否对我们的物品执行？
3. ItemDetached 触发时 IsDestroyed() 是否为 true？
4. 是否存在统一的「物品不再存在」事件？
```

确认前保持手动清理；不要新增 `ItemDestroyed.cs`。

## 如果将来要做

- 只在确认「销毁与移动可区分」后新增钩子；
- 钩子里只调 `DeviceRegistry.Unregister(item)`（该调用不是幂等的；只有确认物品尚未注销时才能调用）；
- 同步更新 `Lifecycle/README.md` 反向索引。

---

# P3-1：文档同步（历史记录）

> 本节记录较早一轮的文档同步（当时测试数 259）；测试数、文件清单与装置状态章节
> 已被 P4-1 和当前 README / `docs/device-state-architecture.md` 取代，保留作为演进记录。

## 已完成

- `README.md`：
  - 删除 `EmitterDevice` / `EmitAll` / `Adopt` / `OnGameLoadedInit` / `Grid.Refresh` / `GetOrCreate` 等旧架构描述；
  - 发射章节改为 `FlowRuntime.Emit` + `FlowStock.Read/Write`；
  - 生命周期表改为 `EscapeUIManager.OnMainMenu` + `QuitToMenu` → `Shutdown`，`EmporiumEntry.OnDestroy` → `Shutdown`；
  - 测试数改为 259，并注明以 `dotnet test` 为准；
  - 视觉章节改为 36 色加权混合（`FlowLiquidPalette`）；
  - 装配件表改为 `device` / `flow_container` / `water_bottle` / `emitter`；
  - 反射与 `reflection-external` 的约束改成与当前实现一致。
- `src/Mod/README.md`：`SaveExited` 改 `Shutdown`，补 `EmporiumDestroyed` / `ItemPickedUp`，装置清理边界。
- `src/Mod/GridBuilding/Lifecycle/README.md`：按当前钩子重写 #1~#9，补 `Reset`/`Shutdown` 分工与反向索引。
- `src/Logic/Devices/README.md`：删除「首次发射的 adopt」，绑定改为物品挂载 / 读档补绑。
- `docs/device-binding-design.md`：标题改为历史设计记录，注明被 tag 化绑定取代的差异。
- `docs/item-setup-refactor.md`：标题改为历史设计记录，注明当前装配件集合与反射发现的偏离。

## 验证命令

```powershell
Select-String -Path README.md,src/Mod/README.md,src/Mod/GridBuilding/Lifecycle/README.md,src/Logic/Devices/README.md `
  -Pattern "EmitterDevice|EmitAll|Adopt|OnGameLoadedInit|230|CloseAllScenes|PatchTargets"
Select-String -Path README.md,src/Mod/GridBuilding/Lifecycle/README.md `
  -Pattern "CloseAllScenes|FlowDriver.Reset"
```

预期：

- 第一条只剩「明确否定旧实现」的句子（`没有 EmitterDevice / EmitAll / Adopt`、`不能用 CloseAllScenes`）；
- 第二条 `CloseAllScenes` 只出现在「不能用」的说明里，`FlowDriver.Reset` 只出现在 #8 / `Reset` 与 `Shutdown` 的分工表里；
- 历史设计稿（`docs/device-binding-design.md`、`docs/item-setup-refactor.md`）仍保留旧术语，但开头已标注「历史设计记录」。

## 仍未覆盖（有意的）

- `docs/flow-visual/**`：历史视觉设计存档，不重写；
- `.modstudio/**`：mod 板的历史记录，路径引用可能过期，由 board 维护者处理；
- `docs/游戏机制_API手册.md`：游戏 API 记录，非本 mod 架构；保留旧取证背景；
- `docs/liquids.md`：游戏液体表，作为数据参考保留。

---

# P4-1：装置状态适配器与 tag 映射（已实施，适配器单例化后）

## 结论

最终边界：

- **Logic** 拥有语义状态、行为、事件：`IFlowDevice` / `IFlowRouter` /
  `IFlowNotifier`；事件是**`Action<IFlowNotifier>`**；
- **Mod / DeviceState** 拥有无状态 `DeviceStateAdapter` 单例（`Create` / `Load` / `Save`）、
  `DeviceDefinition`、`DeviceAdapterCatalog`、`FlowTagNaming` / `FlowPropertyTags`、
  `DeviceStateSync`；
- **Mod / DeviceServices** 拥有 `DeviceHost`（`(Key, Adapter, Device)` 绑定）与
  `DeviceRegistry`；`DeviceHost.Create` 是唯一装置出生路径；
- **Mod / TagMapping** 只保留流源 / 容器的 `FlowProperties` ↔ 原生字段 tag：
  `FlowStock` + 静态 `GameItemTags`；
- **流源 / 容器 tag 是原版字段级协议**：`CURRENT_PART_<ID>`（int µL）、
  `CONTAINER_TEMP_TAG`（double °C），由 `FlowTagNaming.ForProperty` 定义并缓存到 `FlowPropertyTags`；
- **装置私有状态是适配器的一帧**：`Create` 建装置、`Load` 只更新已有实例、
  `Save` 写回 tag，事件驱动，不经过 `FlowStock`。

过渡期曾使用 `ITagStore` + 每装置适配器实例；同一切换中已删除，本文不再描述。

完整说明见 [`device-state-architecture.md`](device-state-architecture.md)。

## 落地文件

| 文件 | 作用 |
|---|---|
| `src/Logic/Devices/Interfaces/IFlowNotifier.cs` | `Changed` / `Exhausted`（`Action<IFlowNotifier>`） |
| `src/Mod/DeviceState/Contracts/DeviceStateAdapter.cs` | `Create` / `Load` / `Save` 契约 + 泛型字段表基类 |
| `src/Mod/DeviceState/Contracts/DeviceStateFields.cs` | `TagFields.Int/Double` 字段表；显式 getter/setter |
| `src/Mod/DeviceState/Naming/FlowTagNaming.cs` | 属性 id + 种类 → tag 名；唯一命名规则 |
| `src/Mod/DeviceState/Naming/FlowPropertyTags.cs` | 启动期属性 → tag 名只读缓存；热路径不再拼字符串 |
| `src/Mod/DeviceState/Catalog/DeviceDefinition.cs` | key / 装置类型 / 单例适配器 |
| `src/Mod/DeviceState/Catalog/DeviceAdapterCatalog.cs` | 扫描并校验适配器；`Keys` / `TryGet` / `Get` / `FindFor` |
| `src/Mod/DeviceState/Sync/DeviceStateSync.cs` | 标脏 + `FlowDriver.Update` 帧末 `SaveAll`；`Exhausted` 同步回写 |
| `src/Mod/DeviceState/Adapters/*StateAdapter.cs` | 8 个无状态适配器：Create / Load / Save 各装置字段 ↔ tag |
| `src/Mod/TagMapping/Core/GameItemTags.cs` | 静态单 tag 工具：读 `modifiedState` / 写 `state` / 等值与零值跳过 |
| `src/Mod/TagMapping/Core/FlowStock.cs` | 流源 / 容器 `FlowProperties` 读写 |
| `src/Mod/Common/Item/ItemTagCommit.cs` | tag 写入后的快照 + 表现统一提交 |
| `src/Mod/DeviceState/Sync/DeviceStateReconcilePatch.cs` | `ValidateShapeState` postfix：未脏 Host 的 `LoadAll()`，不写回 |
| `src/Mod/DeviceServices/Core/DeviceHost.cs` | 唯一出生入口 `Create`；`(Key, Adapter, Device)` 绑定与 `LoadAll` / `SaveAll` |
| `tests/FluxForge.Tests/GameStubs.cs` | 最小 `GameItem` / `TagSystem` / `TagState` / `ItemTagCommit` 测试桩 |
| `tests/FluxForge.Tests/DeviceOrchestrationTests.cs` | Bind → Host / Registry → Load / Save → Dirty / Flush → Exhausted 编排测试 |

## 已删除

`IFlowState`、`FlowTagMap`、`FlowStock.ReadInto`、
`FlowStock.Write(GameItem, IFlowState)`、`FlowDeviceEvents`、`DeviceTagSyncPatch`、
`ITagStore`、`GameItemTagStore`、`FakeTagStore`、
`DeviceAdapterCatalog.CreateDevice` / `CreateAdapter`、`DeviceStateAdapter.Device`，
以及 `IceBlockDevice` / `TemperatureDevice` / `CollectorDevice` 的无参构造
（`IFlowNotifier` 保留，仅去掉 `: IFlowState`）。

## 验证

```powershell
dotnet build FluxForge.csproj -c Release --no-incremental -p:GameManagedDir="E:\SteamLibrary\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"
dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
```

本轮结果：**build 0 warning / 0 error；test 290 / 290 通过**。

## 剩余风险 / 未覆盖

- **实机读档未验证**：适配器回灌依赖 `modifiedState`，仍按 README §9 做实机验收。
- **适配器与编排单测使用最小测试桩**：只覆盖纯 tag 映射、Bind / Flush 等静态流程，
  不覆盖游戏内 `GameItem` 实现、物品生命周期与 patch 时序。
- **`DeviceRegistry` 销毁清理**仍是 P2-2 的有界保留，本轮未加 `ItemDestroyed`。
- **历史设计稿保留旧术语**：`docs/device-binding-design.md` /
  `docs/item-setup-refactor.md` 开头已标注「历史设计记录」，其状态同步章节不代表当前实现；
  `docs/device-state-adapter-singleton-design.md` 是本次迁移的设计稿，结论已落地。
- **不做防御式代码**：缺 tag 读 0、零值不创建 tag、生命周期由绑定/注销保证；
  本轮没有新增 null / try-catch / 不可能状态兜底。
- `FlowStock.Total` 每次发射激活仍会遍历全部液体 tag；这是性能剩余项（P2-1），
  不影响正确性。

---

# 建议实施顺序（更新）

```text
本轮已完成：
  P0-1 构造期注册
  P1-2 SaveExited + EmporiumDestroyed
  P4-1 装置状态适配器单例化 + Create/Load/Save（构建 / 290 单测通过，实机读档待验）
  P3-1 文档同步（历史记录，见上文）

本轮不做：
  P0-1 的「Emit 不变量报错 / 移除 ItemAttached 兜底」→ 需先验证原生网格
  P1-1（接受全 mod 失败策略）
  P2-1（纯性能优化）
  P2-2（ItemDestroyed 不采用，生命周期待调研）
```

P0 与 P1-2 共享同一条实机验证路径：

```text
空网格 + 发射器
→ 正常发射
→ 普通 Escape / QuitToMenu
→ Continue → 仍能发射
```
