# Execution

单轮边执行器。

```text
StorySequence  一轮边注册产生的对话链和完成等待
```

它只追加台词、挂同步动作、等待最后一句结束；不启动事件，也不决定下一个节点。

节点推进和退出由 `../StoryPlayer.cs` 负责。
