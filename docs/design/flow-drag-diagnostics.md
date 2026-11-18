# 单件拖拽后流源偏移：临时诊断

构建：`dotnet build FluxForge.csproj -c Release -p:FlowDiagnostics=true -p:OutputPath=output/flow-diagnostics/`

默认构建不包含自动检查器及 F8 调用。诊断版不调整坐标、不重建登记、不改变流源处理结果。

## 玩家操作

使用诊断版，单件拖拽装置放下，再发射流源。复现后提供游戏目录下的 `Player.log`：

`%USERPROFILE%\AppData\LocalLow\Questing Goose Studio\Probably Stolen`

不需要按键；F8 可额外输出当前现场。

## 判断依据

- 注册：以物品当前 `modifiedShape.ForwardTransform` 和 anchor 标签重新推导预期格子，对照已有登记表，并检查未登记的装置。不会调用 Register 修正现场。
- 视觉：以原版 `UISettings.current.gridSize`、物品容器 `inner` 和逻辑格中心独立计算预期屏幕位置，对照实际本体、边缘、亮核 Image 的几何中心。不调用被检查的 `ReadSnapshot` / `PositionFor`。
- 每轴允许 1 屏幕像素差：现有渲染器对位置和尺寸作像素取整。隐藏面板、尚未建立的绘制层不参加中心检查；拖拽期间主动注销格子是正常行为，完整性检查等到拖拽结束后进行。
- 每块网格首次检出偏差后输出一次自动报告，避免逐帧刷日志。拖拽收尾和发射尝试另外输出现场。
- 相关 Harmony 补丁记录 owner、程序集和方法名，仅作冲突排查线索，不据此认定某个 mod 是原因。

## 原版代码依据

已反编译当前游戏 `Probably Stolen_Data/Managed/Assembly-CSharp.dll`：

- `GameGridInventory.UncheckedAccept`：先 `item.AddParent(this)`，最后才 `item.ValidateShapeState()`。挂载时和落地后的形状需要分别观察。
- `ItemMouseDragHandler.EndDrag`：接受物品、关闭假背景、Validate、结束拖拽；当前 `GetCurrentItem()` 仍返回该物品。现有 EndDrag 补登记保留。
- `GameItemElement.ApplyRectTransform`：使用 `modifiedShape.minX/minY × UISettings.current.gridSize` 放置物品。
- `GameGridInventory.GetGridPosition`：按 `UISettings.current.gridSize` 将 UI 位置换算成格子。

## 验证与边界

诊断版和默认版均编译通过；逻辑测试 520 项通过，包含形状移动后旧登记被检出、旋转镜像及空洞不误报的测试。默认 DLL 内不存在 FlowDiagnostics 类。

尚未在游戏内复现玩家的问题。检查覆盖登记坐标及 Image 几何中心；贴图透明留白、图集内容自身偏心、后续 Canvas 排版或其它 mod 在检查之后修改 UI，仍可能需要进一步检查，不能据此承诺捕捉所有视觉偏差。
