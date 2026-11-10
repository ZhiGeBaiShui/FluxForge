# Contracts

```text
StoryAssembly
  Entry       剧情入口边
  Complete()  内容完成动作
```

剧情是否可播放、何时播放、播哪段，全部由 `StoryEvent` 和选择器决定。
这里不再有 NPC 阶段或 `CanPlay()`。
