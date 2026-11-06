# GridBuilding/Lifecycle：网格生命周期

## 要做什么

### 新增一个生命周期钩子

1. 先判定：它是否改变物品在网格中的位置或归属？
   - 是：在本目录加一个切片（通常一个事件一个文件；对称的 `#3b` / `#3c` 可共用 `ItemPickedUp.cs`）。
   - 否（存档边界、进程启停）：放 `Bootstrap/`。例外是 `EmporiumDestroyed`，它跟着商城主场景销毁，仍放本目录。
2. 写 patch：目标用 `[HarmonyPatch(typeof(游戏类型), nameof(方法))]`；需要多个目标时用 `TargetMethods()`。Prefix / Postfix 只调用 `GridRegistry`、`FlowGridAdapter`、`ItemDeviceBinding`、`DeviceRegistry` 或 `FlowDriver`，不写业务逻辑。
3. 按事件接动作：
   - 物品进入：`ItemDeviceBinding.Bind` + `FlowGridAdapter.Register`；
   - 物品离开或拿起：`FlowGridAdapter.Unregister`；
   - 批量改变形状（排序、读档）：`FlowGridAdapter.Rebuild`；
   - 网格销毁：`FlowDriver.RetireByGrid`；
   - 退出存档 / mod 卸载：`FlowDriver.Shutdown`。
4. 在本页事件表加一行，并更新文末反向索引。
5. 批量拖拽只在 `StartGroupDrag`（`#3c`）注销，不要加结束 / 取消 patch。结束走 `Expel` + `UncheckedAccept`，已有的 `#3` / `#2` 会恢复登记。
6. 跑测试：

   ```powershell
   dotnet test tests\FluxForge.Tests\FluxForge.Tests.csproj -c Release
   ```

### 排查「物品拿走后流源还撞装置」

先确认 `#3b`（单件 `StartDrag`）和 `#3c`（批量 `StartGroupDrag`）都命中；再看 `#3` 的 Prefix 是否在 `RemoveParent` 清空 `parentInventory` 之前取到了旧网格。

## 本目录的事件（#1~#4、#7）

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 1 | 网格出现 | `GridCreatedPatch.cs` | `[HarmonyPatch(typeof(GameGridInventory))]` + `TargetMethods()`：全部 public 构造函数 + 全部 public `SetShape` 重载 | `GridRegistry.Register`（幂等） |
| 2 | 物品进入 | `ItemAttached.cs` | `GameItem.AddParent`（Postfix，`__result == true` 才继续） | `ItemDeviceBinding.Bind` + `FlowGridAdapter.Register` |
| 3 | 物品离开 | `ItemDetached.cs` | `GameItem.RemoveParent`（Prefix） | `FlowGridAdapter.Unregister` |
| 3b | 物品拿起 | `ItemPickedUp.cs` | `ItemMouseDragHandler.StartDrag`（Postfix） | `FlowGridAdapter.Unregister` |
| 3c | 批量框选拿起 | `ItemPickedUp.cs` | `ItemMultiSelectHandler.StartGroupDrag`（Postfix，注入 `___selectedItems`） | 对全部选中物品 `FlowGridAdapter.Unregister` |
| 3d | 单件拖拽结束 / 中止 | `ItemDropped.cs` | `ItemMouseDragHandler.EndDrag`（Postfix）/ `AbortDrag`（Prefix 取物品 + Postfix） | `FlowGridAdapter.Register` 幂等补登记 |
| 3e | 批量拖拽恢复 | `ItemDropped.cs` | `ItemMultiSelectHandler.RestoreGhosts`（Postfix，注入 `___selectedItems`） | 对全部仍属于网格的物品补登记 |
| 4 | 排序 | `InventorySorted.cs` | `InventorySortHelper.Sort`（Postfix，`__result == true` 才继续） | `FlowGridAdapter.Rebuild` |
| 7 | 网格销毁 | `GridLifetime.cs` | 网格 `rectTransform` 下的哨兵节点 `OnDestroy` | `FlowDriver.RetireByGrid` |

### #1 的实现约束

- **一个 patch 类，不是多个**：`[HarmonyPatch(typeof(GameGridInventory))]` 只有一个，所有目标由 `TargetMethods()` 一起产出，共用一个 `Postfix`。
- **只枚举 public 构造**：私有无参构造只建 handler / inner / background 等节点，此时 `inventoryShape` 还是 `null`，不能注册。
- **不把 `Type` 传给反射辅助方法**：直接写 `typeof(GameGridInventory).GetConstructors()` 和 `typeof(GameGridInventory).GetMethods(...)`。对计算出的 `Type` 做反射会被 ModScanner 判为 `reflection-external`，而 `manifest.xml` 的 `<Capabilities />` 为空。
- **不变量**：`FlowRuntime` 的生命周期 == `GameGridInventory` 的生命周期。构造函数 Postfix 覆盖新档、读档和场景内新建网格；`SetShape` 覆盖运行时扩容 / 升级，重复 `Register` 幂等。

## 不在本目录的事件

存档边界和进程生命周期在 `Bootstrap/`：

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 5 | 退出存档 | `Bootstrap/SaveExited.cs` | `EscapeUIManager.OnMainMenu`（普通 Escape）/ `GameMaster.QuitToMenu`（团灭、调试）Postfix | `FlowDriver.Shutdown` |
| 6 | 读档完成 | `Bootstrap/SaveLoaded.cs` | `ModHook.OnGameLoadedLate` | `KillAll` + `ItemDeviceBinding.Bind` + `Grid.Rebuild` |
| 8 | 场景卸载 | `Bootstrap/ModHost.cs` | `SceneManager.sceneUnloaded` | `FlowDriver.Reset` |
| 9 | mod 卸载 | `Bootstrap/FFModEntry.cs` | `IMod.OnDisable` | `FlowDriver.Shutdown` + `FlowVisualLayer.Shutdown` |

`#5b` 商城主场景销毁不在 `Bootstrap/`，但也不是网格内的位置变化，单独列在这里：

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 5b | 商城场景销毁 | `EmporiumDestroyed.cs`（本目录） | `EmporiumEntry.OnDestroy` Postfix（守卫 `EmporiumEntry.Instance != __instance`） | `FlowDriver.Shutdown` |

### 关键约定

- **#5 不能用 `PlayerStore.Reset`**：它同时被 `PlayerStore` 构造函数和 `PreLoadStore` 调用；`EmporiumEntry.Start` 构造展示窗 / 柜台网格时会触发它，把刚登记的 runtime 清掉。
- **#5 不能用 `GameMaster.CloseAllScenes`**：它在「开始游戏」时也会被调用，会在与退档无关的时刻清空登记。
- **#5 的 `Shutdown` 会清空 `GridRegistry`**：退出存档后网格对象随场景销毁；下一个存档由 `#1` 在构造期重新注册。
- **#5b 是场景级兜底**：`Shutdown` 可重复调用，和 `#5` 先后命中同一次退出也安全。
- **#6 会与 `#2` 重叠**：读档恢复也走 `AddParent`；`SaveLoaded` 仍无条件补一次 `Bind` + `Rebuild`，因为 `AddParent` 之后 `modifiedShape` 才最终生效。重复 `Bind` 由 `DeviceRegistry.GetHost` 的已有 Host 分支 / `Rebuild` 的整表重写保证幂等。
- **#8 的 `Reset` 不清理网格登记**：场景卸载不等于网格销毁，清掉会让仍存活的网格变成死网格；真正销毁走 `#7`。
- **#9 的顺序**：先停宿主，再 `FlowDriver.Shutdown()`，最后 `FlowVisualLayer.Shutdown()`。

## Reset 与 Shutdown 的分工

| 对比项 | `FlowDriver.Reset`（#8） | `FlowDriver.Shutdown`（#5 / #5b / #9） |
|---|---|---|
| 流源 | 存活 runtime `KillAll`；已死 runtime 直接退役 | 不清流源，直接释放视图（网格随后销毁） |
| 网格登记 | **保留** `GridRegistry` | **清空** `GridRegistry` |
| 装置登记 | `DeviceRegistry.Clear` | `DeviceRegistry.Clear` |
| 视觉视图 | 存活 runtime 保留；已死 runtime 的视图随退役释放 | 逐个 `Dispose`，引用置 `null` |
| 用途 | 场景切换（网格对象可能还活着） | 退出存档 / 退档兜底 / mod 卸载 |

## 已知边界：DeviceRegistry 没有物品销毁钩子

`ItemDetached` / `ItemPickedUp` 只清 `FlowGridAdapter` 的格表，**不注销 `DeviceRegistry`**：物品可能只是被拿起或移动，装置要跟着物品走。当前没有 `ItemDestroyed` patch，所以 `DeviceRegistry` 的条目只在以下时点清理：

```text
DeviceRegistry.Clear()
  ← FlowDriver.Reset（#8）/ FlowDriver.Shutdown（#5、#5b、#9）
```

或装置主动 `Exhausted`。这是有界保留（每个会话放置过的装置数量），不是即时销毁钩子。

## 相关 API

| API | 提供 |
|---|---|
| `GridRegistry.Register` / `Get` / `Unregister` / `Clear` | 网格 ↔ `FlowRuntime` 的登记与查找 |
| `FlowGridAdapter.Register` / `Unregister` / `Rebuild` | 物品 anchor 格表的增量维护与整表重建 |
| `FlowGridAdapter.Contains` / `GetRouterAt` / `GetReactorAt` | 运动层按格查询网格边界与装置 |
| `ItemDeviceBinding.Bind` | 确保物品的装置实例存在；重复调用安全 |
| `FlowDriver.Update` / `LateUpdateVisuals` | 每帧推进流源 / 视觉 |
| `FlowDriver.RetireByGrid` / `Reset` / `Shutdown` | 单网格退役、场景边界、退档 / 卸载边界 |

## 反向索引：服务被谁用

| 服务 | 使用方 |
|---|---|
| `GridRegistry.Register` | #1、#2（登记被清空时兜底） |
| `GridRegistry.Get` | #2、#3、#3b、#3c、#4 |
| `GridRegistry.Runtimes` | #6、#8、#9（直接遍历或由 `FlowDriver` 取快照） |
| `ItemDeviceBinding.Bind` | #2、#6 |
| `FlowGridAdapter.Register` | #2（单件）、#3d / #3e（拖拽收尾补登记）、#4 / #6（经 `Rebuild` 内部的 `RegisterCore`） |
| `FlowGridAdapter.Unregister` | #3、#3b、#3c |
| `FlowGridAdapter.Rebuild` | #4、#6 |
| `FlowDriver.RetireByGrid` | #7 |
| `FlowDriver.Reset` | #8 |
| `FlowDriver.Shutdown` | #5、#5b、#9 |

`ItemDeviceBinding.Bind` 被 #2 / #6 共用，`Rebuild` 被 #4 / #6 共用，`Unregister` 被 #3 / #3b / #3c 共用：语义相同，不拆。批量拖拽结束走 `Expel` + `UncheckedAccept`，由 `#3` / `#2` 感知，所以 `#3c` 只负责拿起。

## 为什么 / 不做会怎样

- 漏掉 `#2` / `#3` / `#3b` / `#3c` / `#3d` / `#3e` / `#4` / `#6` 中任意一环，`FlowGridAdapter` 的格表就会过期：物品已经被拿起、移动或排序，流源仍会撞到旧格上的装置。
- 把 `#5` 挂到 `PlayerStore.Reset` 或 `GameMaster.CloseAllScenes`，会在开始游戏 / 读档过程中清空刚登记的 runtime，网格直接失效。
- 用 `Reset` 清网格登记，场景卸载后仍存活的网格会变成没有运行环境的死网格。
- 单件拖拽成功时 `EndDrag` 仍会走 `Expel` + `UncheckedAccept` 的 `#3` / `#2`；`#3d` 的补登记必须幂等，不能假设 `AddParent` 一定没发生。批量拖拽的 `#3e` 同理，只处理“恢复到原容器/原位”的路径。
- `#1` 把计算出的 `Type` 传给反射辅助方法，会被 ModScanner 判为 `reflection-external`；`<Capabilities />` 为空时直接拒绝加载。
- 期望物品销毁时自动清 `DeviceRegistry` 会留下静态引用；当前只有 `Clear` / `Exhausted` 两个清理点。
