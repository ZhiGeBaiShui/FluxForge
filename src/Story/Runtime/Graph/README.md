# Graph

剧情图的数据结构。

```text
StoryNode   一个暂停节点，只有出边
StoryEdge   一条边：选项文字、图标、执行逻辑、起点和终点
```

节点只描述“停在哪里”，边只负责“选定后注册什么台词和动作”。

边默认跳到 `Connect` 指定的终点；需要按执行结果分支时使用：

```text
edge.Branch(condition, whenTrue, whenFalse)
```

`StoryPlayer` 在 `edge.Execute()` 之后调用 `ResolveNext()`，因此条件可以读取本次执行写入的状态。

边不再携带空桌检查、网格锁或事件调用。
