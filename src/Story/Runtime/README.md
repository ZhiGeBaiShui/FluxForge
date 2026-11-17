> 当前状态索引：2026-11-16，见[项目状态](../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Story Runtime

通用剧情运行时。管理入口与其专用辅助类分目录，公共模型和纯选择规则按职责共享。

```text
Bootstrap/             组合根
Patches/               剧情层独立的原版生命周期补丁
Model/                 选择与执行共用的数据定义
Selection/             到访规则 / 剧情事件选择（纯逻辑，可单测）
State/                 modData NPC 状态适配
Graph/                 StoryNode / StoryEdge
Contracts/             StoryAssembly
Direction/
  NpcStoryHost.cs       NPC 到访与事件阶段管理入口
  Services/            来访阶段装配、活动剧情与交易结果桥接
Playback/
  StoryPlayer.cs       剧情播放管理入口
  Execution/           对话序列与执行辅助
  Dialogue/            原版 Dialogue 选项装配、逐行停顿
```

业务注册入口：`StoryBootstrap`、`NpcStoryContent`、`StoryAssembly`。
新增内容先读 `../README.md`；运行器看 `Playback/StoryPlayer.cs`；
NPC 生命周期看 `Direction/NpcStoryHost.cs`。

退出存档由 `Patches/StorySaveExitPatches.cs` 独立接入原版两个退出入口，在前置补丁中清理剧情；不复用其他层的退出钩子。
