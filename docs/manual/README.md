> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 装置手册

当前为七页独立手册：概览、发射器、过滤、冷萃基础、冷萃进阶、冷萃自动化、离心。

UI从 `mod/AssetBundles/fluxforge_device_manual` 加载 `FlowDeviceBasicManual`，由 `DeviceManual` 排页、绑定按钮和本地化。不是旧的空白四页骨架，也不克隆原版书页。

Unity源预制体：`E:/code/ProbablyMod/Assets/FluxForgeAssets/FlowDeviceBasicManual.prefab`；构建命令为Unity菜单 `FluxForge/Build Device Manual AssetBundle`。生成资源后不再脚本重排用户布局，按用户构建结果部署。

| 预制体页面 | 演示区域 | 事件 | 网格 |
|---|---|---|---|
| Page_coldtrap | Demo_cold_trap_loop | RunColdTrapLoop | 7×3 |
| Page_coldtrap_manual | Demo_coldtrap_manual_advanced | RunColdTrapAdvancedLoop | 11×5 |
| Page_coldtrap_auto | Demo_coldtrap_auto_advanced | RunColdTrapAutomation | 9×5 |

进阶右侧仅发射一次，左侧接收后2秒清理。自动化流源进入冷萃后补左下转角，形成闭环后10秒结束。按钮应启用Interactable，在演示区域上方排布；区域内节点固定为 `GridArea/manual.demo.placeholder` 和 `PlayButton`。

`ManualDemoBinding` 绑定事件与网格尺寸；`ManualDemoPage` 缩放网格、禁用交互，翻页/关闭时取消事件、退役流源并清理网格。`DemoPlacement` 使用Simple图片模式，排除原版Tiled的窄边重复绘制路径。视觉效果仍需游戏内复测。

细节见 [src/Manual](../../src/Manual/README.md) 与[当前状态](../current-project-state.md)。

2026-11-17：手册文本已改为原版ModHelper按当前语言查询外置Localization/zh.csv与en.csv。奥雅第一张便条已改为原版Books/starter_note.json图文节点，不再读取写死中文的整张PNG。详见[本地化接入](../design/item-and-note-localization.md)。
