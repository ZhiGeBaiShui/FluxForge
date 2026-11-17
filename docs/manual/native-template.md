> 维护校对：2026-11-16。当前实现以[项目状态](../current-project-state.md)及源码为准；历史方案和审查结论保留其当时上下文。未被工程使用的设计图片、试听文件和审查中间产物已列入清理清单；批量删除被工具策略拒绝，尚未删除。

# 原版模板取证

从本机游戏 level1 资源读取：MultiPageUIManager pathID 11906；panel 1167；books[3] = BookChemistry（1238）；左右按钮 577/989。

自定义book作为原版manager.books的新项，不克隆manager，原版OpenUI/ShowPage/Flip/Close全部继续使用原组件；原版panel的拖动/页面贴图/页码逻辑不修改。

页面克隆BookChemistry/Page2。已核实际路径：Title (3)、Title (4)、Section1/SectionDivider (7)/Text (TMP) (2)、Section1/Text (TMP) (2)、Section2。保留标题、说明段落模板，隐藏原版化学插图和无关步骤。Section2作为演示区域。

演示按钮克隆原版manager.buttonRight，替换Button.onClick事件，增加克隆的章节标题文本作为演示标注；不复制原版翻页事件。禁用克隆LocalizeStringEvent并清除其更新回调；TMPLocalizer保持ChangeFontOnly，继续跟随原版字体角色。

本文件只记录静态资源调查。仍需游戏内检验文字容量、演示按钮实际大小和嵌入网格的位置。
