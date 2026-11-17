> 当前状态索引：2026-11-16，见[项目状态](../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

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
      ColdTrapAdvancedPage.cs    直接绑定进阶/自动化预制体页
      CentrifugePage.cs
```

新增一本书时，在 `Books/` 下创建自己的目录；该书的页面绑定放在自己的 `Pages/` 中。

- `ManualAttachment`：物品装配件，将阅读行为接入手册目录。由物品装配系统扫描程序集自动发现。
- `ManualCatalog`：按物品标签选择手册。
- `DeviceManual`：加载手册预制体，编排页面、绑定通用按钮、加载文本和字体。
- `ManualDemoPage`：装配演示网格，通过 `FlowEventApi` 启动与取消演示。
- `DeviceManualUiPatch`：协调原版文档界面和自定义手册的关闭行为。

页面排版来自 Unity 预制体，中英文正文来自 `mod/Localization/zh.csv` 和 `en.csv`。
事件编排仍属于 `src/Events/`；物品注册与通用装配属于 `src/ItemIntegration/`。

入门便条物品 ID 为 `fluxforge_starter_note`，物品统一由项目自己的 attachment 工厂注册。
便条内容位于 `mod/Books/starter_note.json`，使用原版PaperBookRegistry/PaperUI。
图像节点引用现有装置贴图，文字节点以loc从外置CSV读取；不再读取旧的中文整张PNG。
便条JSON不声明item，保留现有attachment工厂作为唯一物品注册入口。
PaperNoteContent仅通过原版OnOpened回调设置两台发射器朝向，原版重建后会再次设置。
退出存档/卸载沿用现有Clear入口关闭本便条，不销毁原版其他书籍。

## 当前演示约定（2026-11-16）

手册七页；两张进阶页直接绑定用户构建的预制体，不再克隆基础页。进阶11×5只从右侧发射一次；自动化9×5在进入冷萃后补入口转角，闭环10秒结束。演示图片为Simple。页面按钮和布局在Unity源预制体编辑，详见[手册文档](../../docs/manual/README.md)。
