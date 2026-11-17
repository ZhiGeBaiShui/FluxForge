> 当前状态索引：2026-11-16，见[项目状态](../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Playback

`StoryPlayer.cs` 是播放管理入口；辅助类放在子目录。

```text
Execution/   对话序列与执行辅助
Dialogue/    原版 Dialogue 输出、选项与停顿适配
```

NPC 到访与事件阶段由 `../Direction/NpcStoryHost.cs` 管理。
共享节点定义在 `../Graph/`，内容装配契约在 `../Contracts/`。
