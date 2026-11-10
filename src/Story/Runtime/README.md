# Story Runtime

通用剧情运行时。

```text
Model/        选择与执行共用的纯数据
Selection/    到访规则 / 剧情事件选择（纯逻辑，可单测）
State/        modData NPC 状态适配
Direction/    通用 NPC 控制器、访客会话、交易完成入口
Bootstrap/    组合根
Graph/        节点与边
Execution/    单轮边执行
Dialogue/     原版 Dialogue 装配、逐行停顿
Contracts/    StoryAssembly
StoryPlayer   单段剧情执行
```

调用方只接触：

```text
StoryBootstrap   启动/关闭
NpcStoryContent  身份 + 规则 + 事件
StoryAssembly    剧情内容
```
