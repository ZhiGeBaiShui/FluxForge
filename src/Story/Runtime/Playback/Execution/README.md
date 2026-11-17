> 当前状态索引：2026-11-16，见[项目状态](../../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Execution

StorySequence注册Line/Action/Call/Insertion有序步骤；StoryCallFrame保存子图游标与返回节点。
StoryInsertionCursor使用原有StoryEventSelector规则，逐个到达候选时读取最新条件。
StoryPlaybackContext共享NPC注册表、来访上下文和互斥组。

StoryPlayer在上层Playback目录；此目录只放它调用的执行辅助类型，Steps集中实际步骤类型。
