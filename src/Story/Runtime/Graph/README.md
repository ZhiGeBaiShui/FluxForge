> 当前状态索引：2026-11-16，见[项目状态](../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Graph

剧情图的数据结构。

StoryEdge的label参数是外置CSV键，不是显示文字；Label属性在构建菜单时按当前语言解析。无选项的入口边允许null。对白同样在Execute中解析，避免图构建时缓存旧语言。

```text
StoryNode   一个暂停节点，只有出边
StoryEdge   一条边：选项文字、图标、执行逻辑与下一节点
```

节点只描述“停在哪里”，边只负责“选定后注册什么台词和动作”。

边默认跳到 `Connect` 指定的终点；需要按执行结果分支时使用：

```text
edge.Branch(condition, whenTrue, whenFalse)
```

`StoryPlayer` 在 `edge.Execute()` 之后调用 `ResolveNext()`，因此条件可以读取本次执行写入的状态。子调用前快照下一节点，返回不再重算父边。

边不再携带空桌检查、网格锁或事件调用。
