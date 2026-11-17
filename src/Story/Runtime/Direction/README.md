> 当前状态索引：2026-11-16，见[项目状态](../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Direction

`NpcStoryHost.cs` 是 NPC 到访、事件阶段和离场结果的管理入口。
Dispose退订事件并移除注册NPC的排队客户；匹配的当前客户通过原版DismissCurrentClient释放。
Add 同时把 NPC 工厂登记到原版 `StoreClientListDict.storeClientDict`；Dispose 注销工厂并清空原版名字缓存。
原版按 ID 创建使用 `NpcStoryContent.DirectVisitRule`，跳过自然来访的条件、概率和冷却；剧情仍在 OnArrival 按当前存档选择。
工厂及名字查询只创建客户，不读取存档、不推进剧情、不发放物品。自然来访继续使用 VisitRuleSelector。
卸载顺序是先由StoryNpcRuntime取消活动播放，再释放客户；没有PlayerStore实例时不触发其懒创建。

```text
Services/
  NpcVisitAssembly.cs  主剧情、交易后剧情、离场前剧情的顺序装配
  StoryNpcRuntime.cs   活动播放器、取消入口和交易结果桥接
```

播放由 `../Playback/StoryPlayer.cs` 执行；到访和剧情选择规则在 `../Selection/`。
