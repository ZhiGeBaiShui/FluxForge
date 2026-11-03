# GridBuilding/Lifecycle：网格生命周期

一个文件一个事件，只写「什么时候、按什么顺序调」，实现放在 `GridBuilding/Services/`。

## 本目录的事件（#1~#4、#7）

| # | 时机 | 文件 | 钩子 | 动作 |
|---|---|---|---|---|
| 1 | 网格出现 | `GridCreatedPatch.cs` | `GameGridInventory.SetShape` | `GridRegistry.Register` |
| 2 | 物品进入 | `ItemAttached.cs` | `GameItem.AddParent` | `Bind` + `Register` |
| 3 | 物品离开 | `ItemDetached.cs` | `GameItem.RemoveParent` | `Unregister` |
| 4 | 排序 | `InventorySorted.cs` | `InventorySortHelper.Sort` | `Rebuild` |
| 7 | 网格销毁 | `GridLifetime.cs` | `GridLifetime.OnDestroy` | `FlowDriver.RetireByGrid` |

## 不在本目录的事件

存档边界和进程生命周期在 `Bootstrap/`：

| # | 时机 | 文件 | 钩子 |
|---|---|---|---|
| 5 | 退出存档 | `Bootstrap/SaveExited.cs` | `GameMaster.CloseAllScenes` |
| 6 | 读档完成 | `Bootstrap/SaveLoaded.cs` | `ModHook.OnGameLoadedLate` |
| 8 | 场景卸载 | `Bootstrap/ModHost.cs` | `SceneManager.sceneUnloaded` |
| 9 | mod 卸载 | `Bootstrap/FFModEntry.cs` | `IMod.OnDisable` |

## 新增路径的判定

如果新的改动会**改变物品在网格中的位置或归属**，就在本目录加一个切片（或在已有切片里补一次调用）。

判断标准：它会不会让 `FlowGridAdapter._cells` 过期。

## 反向索引：服务被谁用

| 服务 | 使用方 |
|---|---|
| `GridRegistry.Register` | #1 |
| `GridRegistry.Get` | #2、#3、#4、#6 |
| `ItemDeviceBinding.Bind` | #2、#6 |
| `FlowGridAdapter.Register` | #2（单件）、#4 / #6（经 `Rebuild`） |
| `FlowGridAdapter.Unregister` | #3 |
| `FlowGridAdapter.Rebuild` | #4、#6 |
| `FlowDriver.Reset` | #5、#8、#9 |
| `FlowDriver.RetireByGrid` | #7 |

这张表回答「两条不相关的链路在哪里交汇」：

- `ItemDeviceBinding.Bind` 被 #2 和 #6 共用——语义相同（让装置实例存在），不拆
- `FlowGridAdapter.Rebuild` 被 #4 和 #6 共用——语义相同（按当前 `modifiedShape` 重算格表），不拆

如果哪天某个服务开始出现 `if (caller == ...)`，那才是职责过宽，该拆。
