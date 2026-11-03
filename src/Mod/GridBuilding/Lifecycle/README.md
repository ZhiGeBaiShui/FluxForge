# GridBuilding/Lifecycle：网格生命周期

一个文件一个事件，只写「什么时候、按什么顺序调」，实现放在 `GridBuilding/Services/` 或 `Bootstrap/`。

## 本目录的事件（#1~#4、#7）

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 1 | 网格出现 | `GridCreatedPatch.cs` | `[HarmonyPatch(typeof(GameGridInventory))]` + `TargetMethods()`：全部 public 构造函数 + 全部 public `SetShape` 重载 | `GridRegistry.Register`（幂等） |
| 2 | 物品进入 | `ItemAttached.cs` | `GameItem.AddParent`（Postfix，`__result == true` 才继续） | `ItemDeviceBinding.Bind` + `FlowGridAdapter.Register` |
| 3 | 物品离开 | `ItemDetached.cs` | `GameItem.RemoveParent`（Prefix） | `FlowGridAdapter.Unregister` |
| 3b | 物品拿起 | `ItemPickedUp.cs` | `ItemMouseDragHandler.StartDrag`（Postfix） | `FlowGridAdapter.Unregister` |
| 4 | 排序 | `InventorySorted.cs` | `InventorySortHelper.Sort`（Postfix，`__result == true` 才继续） | `FlowGridAdapter.Rebuild` |
| 7 | 网格销毁 | `GridLifetime.cs` | 网格 `rectTransform` 下的哨兵节点 `OnDestroy` | `FlowDriver.RetireByGrid` |

### #1 的实现约束

- **一个 patch 类，不是多个**：`[HarmonyPatch(typeof(GameGridInventory))]` 只有一个，所有目标由 `TargetMethods()` 一起产出；所有目标共用同一个 `Postfix`。
- **只枚举公开构造**：私有无参构造只建 handler / inner / background 等节点，此时 `inventoryShape` 仍是 `null`，不能注册。
- **不把 `Type` 传给反射辅助方法**：直接 `typeof(GameGridInventory).GetConstructors()` 和 `typeof(GameGridInventory).GetMethods(...)`。游戏 ModScanner 会把「对计算出的 `Type` 做反射」判为 `reflection-external`，而 `manifest.xml` 的 `<Capabilities />` 目前为空。新增反射时不要引入 `Type` 参数的通用 helper。
- **不变量**：`FlowRuntime` 的生命周期 == `GameGridInventory` 的生命周期。构造函数 Postfix 已覆盖新档、读档、场景内新建网格；`SetShape` 覆盖运行时扩容/升级。

## 不在本目录的事件

存档边界和进程生命周期在 `Bootstrap/`：

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 5 | 退出存档 | `Bootstrap/SaveExited.cs` | `EscapeUIManager.OnMainMenu`（普通 Escape）/ `GameMaster.QuitToMenu`（团灭、调试）Postfix | `FlowDriver.Shutdown` |
| 6 | 读档完成 | `Bootstrap/SaveLoaded.cs` | `ModHook.OnGameLoadedLate` | `KillAll` + `ItemDeviceBinding.Bind` + `Grid.Rebuild` |
| 8 | 场景卸载 | `Bootstrap/ModHost.cs` | `SceneManager.sceneUnloaded` | `FlowDriver.Reset` |
| 9 | mod 卸载 | `Bootstrap/FFModEntry.cs` | `IMod.OnDisable` | `FlowDriver.Shutdown` + `FlowVisualLayer.Shutdown` |

**#5b 商城场景销毁**不在 `Bootstrap/`，但也不是网格内的位置变化，所以单独列在这里：

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 5b | 商城场景销毁 | `EmporiumDestroyed.cs`（本目录） | `EmporiumEntry.OnDestroy` Postfix（守卫 `EmporiumEntry.Instance != __instance`） | `FlowDriver.Shutdown` |

关键约定：

- **#5 不能用 `PlayerStore.Reset`**：它同时被 `PlayerStore` 构造函数和 `PreLoadStore` 调用；`EmporiumEntry.Start` 构造展示窗/柜台网格时 `PlayerStore.Instance` 首次创建会触发它，导致刚登记的 runtime 被 `Shutdown` 清掉。
- **#5 不能用 `GameMaster.CloseAllScenes`**：它在「开始游戏」时也会被调用，用它做清理会在进入存档后立刻执行，把刚注册的网格全部清掉。
- **#5 的 `Shutdown` 会清空 `GridRegistry`**：退出存档后网格对象随场景销毁；下一个存档由 #1 在构造期重新注册。
- **#5b 是场景级兜底**：原版退档不会逐个调用 `GameItemElement.Destroy`，`GameItem` 是普通 C# 对象，靠失去引用后 GC；正常 Escape 的 `EmporiumEntry.ResetData()` 会 `Object.Destroy(Instance)`，因此 #5b 会在 #5 之后再次触发。`FlowDriver.Shutdown` 可重复调用，重叠安全。
- **#6 会与 #2 重叠**：读档恢复走 `SaveManager.DecodeNodes` → `GameInventory.UncheckedAccept` → `GameItem.AddParent`（反编译确认：`.tmp-decomp/GameGridInventory.cs` 的 `UncheckedAccept` 末尾调 `item.AddParent(this)`），所以 `ItemAttached` 会触发。`SaveLoaded` 仍然无条件补一次 `Bind` + `Rebuild`，作为「读档后 `modifiedShape` 才最终生效」的兜底；重复 `Bind` 由 `DeviceRegistry.Contains` / `Rebuild` 的整表重写保证幂等。
- **#9 覆盖 mod 卸载**：`OnDisable` 先停宿主，再 `FlowDriver.Shutdown()`，最后 `FlowVisualLayer.Shutdown()`。

## Reset 与 Shutdown 的分工

| 对比项 | `FlowDriver.Reset`（#8） | `FlowDriver.Shutdown`（#5 / #5b / #9） |
|---|---|---|
| 流源 | 存活 runtime `KillAll`；已死 runtime 直接退役 | 不清流源，直接释放视图（网格随后销毁） |
| 网格登记 | **保留** `GridRegistry` | **清空** `GridRegistry` |
| 装置登记 | `DeviceRegistry.Clear` | `DeviceRegistry.Clear` |
| 视觉视图 | 存活 runtime 保留；已死 runtime 的视图随退役释放 | 逐个 `Dispose`，引用置 `null` |
| 用途 | 场景切换（网格对象可能还活着） | 退出存档 / 退档兜底 / mod 卸载 |

`Reset` 不能清网格登记：场景卸载不等于网格销毁，清掉会让仍存活的网格变成死网格。`Shutdown` 必须清：退出存档的目标就是清掉全部 mod 引用。

## 已知边界：DeviceRegistry 没有物品销毁钩子

`ItemDetached` / `ItemPickedUp` 只清 `FlowGridAdapter` 的格表，**不注销 `DeviceRegistry`**——物品可能是被拿起或移动，装置要跟着物品走，不能在这里销毁。当前没有 `ItemDestroyed` patch（原方案评估后未采用），所以 `DeviceRegistry` 里的条目只在以下时点清：

```text
DeviceRegistry.Clear()
  ← FlowDriver.Reset（#8）/ FlowDriver.Shutdown（#5、#5b、#9）
```

或装置主动 `Exhausted`。这是**有界的保留**（每个会话放置过的装置数量），不是静态误报；详见 `docs/review-pending-fixes.md` P2-2。

## 新增路径的判定

如果新的改动会**改变物品在网格中的位置或归属**，就在本目录加一个切片（或在已有切片里补一次调用）。

判断标准：它会不会让 `FlowGridAdapter._cells` 过期。

## 反向索引：服务被谁用

| 服务 | 使用方 |
|---|---|
| `GridRegistry.Register` | #1、#2（登记被清空时兜底） |
| `GridRegistry.Get` | #2、#3、#3b、#4、#6 |
| `ItemDeviceBinding.Bind` | #2、#6 |
| `FlowGridAdapter.Register` | #2（单件）、#4 / #6（经 `Rebuild` 内部的 `RegisterCore`） |
| `FlowGridAdapter.Unregister` | #3、#3b |
| `FlowGridAdapter.Rebuild` | #4、#6 |
| `FlowDriver.RetireByGrid` | #7 |
| `FlowDriver.Reset` | #8 |
| `FlowDriver.Shutdown` | #5、#5b、#9 |

这张表回答「两条不相关的链路在哪里交汇」：

- `ItemDeviceBinding.Bind` 被 #2 和 #6 共用——语义相同（让装置实例存在），不拆；
- `FlowGridAdapter.Rebuild` 被 #4 和 #6 共用——语义相同（按当前 `modifiedShape` 重算格表），不拆；
- `FlowGridAdapter.Unregister` 被 #3 和 #3b 共用——语义相同（物品不再落在网格里），不拆。

如果哪天某个服务开始出现 `if (caller == ...)`，那才是职责过宽，该拆。
