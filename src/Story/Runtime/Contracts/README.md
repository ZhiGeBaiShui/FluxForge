# Contracts

剧情框架的静态契约。

```text
StoryAssembly       剧情装载类基类：Entry + Complete
NpcStoryAssembly    NPC 剧情装载类：额外声明 Phase + CanPlay
StoryPhase          Arrival / SecondVisit / GenericVisit / SoldOut
```

这些类不执行剧情，也不依赖具体 NPC。
