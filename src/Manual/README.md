# Manual

顶层手册层，负责手册物品阅读入口、页面编排、预制体加载、本地化和演示区域。

```text
ManualCatalog.cs                 按标签选择书籍
Items/                          阅读行为装配
  ManualAttachment.cs           自定义多页手册
  PaperNoteAttachment.cs        原版通用便条窗口的图片正文（paperImage）
Shared/                         多本书可复用的演示绑定和网格组件
Books/
  DeviceManual/                 流源装置手册
    DeviceManual.cs             打开、关闭、页序和通用按钮
    DeviceManualUiPatch.cs      原版窗口协同
    Pages/                      每页自己的预制体节点与事件绑定
      OverviewPage.cs
      EmitterPage.cs
      FilterPage.cs
      ColdTrapPage.cs
      CentrifugePage.cs
```

新增一本书时，在 `Books/` 下创建自己的目录；该书的页面绑定放在自己的 `Pages/` 中。

- `ManualAttachment`：物品装配件，将阅读行为接入手册目录。由物品装配系统扫描程序集自动发现。
- `ManualCatalog`：按物品标签选择手册。
- `DeviceManual`：加载手册预制体，编排页面、绑定通用按钮、加载文本和字体。
- `ManualDemoPage`：装配演示网格，通过 `FlowEventApi` 启动与取消演示。
- `DeviceManualUiPatch`：协调原版文档界面和自定义手册的关闭行为。

页面排版来自 Unity 预制体，中文正文来自 `mod/Localization/zh-CN.csv`。
事件编排仍属于 `src/Events/`；物品注册与通用装配属于 `src/ItemIntegration/`。

入门便条物品 ID 为 `fluxforge_starter_note`，物品统一由项目自己的 attachment 工厂注册。
教学图在 `mod/Textures/Paper/starter_note.png`。阅读使用导师便条相同的
`PaperUIManager.OpenUI`；`PaperNoteContent` 只叠加图片正文与浅色底，沿用原版窗口按钮。
打开其他便条、关闭、退出存档或卸载时清理图片并恢复原版背景。
