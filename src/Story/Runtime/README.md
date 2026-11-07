# Story Runtime

剧情框架的通用运行时。

```text
StoryPlayer.cs    统筹入口：启动剧情、执行边、推进节点、退出
Contracts/        剧情装载契约
Graph/            节点与边
Execution/        单轮边执行器
Dialogue/         节点到原版 Dialogue 的装配
```

`StoryPlayer` 不逐句播放对话；它只统筹一轮又一轮的边执行。
