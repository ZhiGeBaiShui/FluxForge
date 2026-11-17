> 当前状态索引：2026-11-16，见[项目状态](../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# 剧情分支验证

运行：

```powershell
dotnet test tests/FluxForge.Story.Tests/FluxForge.Story.Tests.csproj -c Release
```

编译实际的 StoryNode / StoryEdge / StoryPlayer / StorySequence / StoryDialogueAdapter、
事件与到访选择器、NpcStoryHost、来访阶段编排、薇拉剧情装配、医院检查和结算、合同状态、原料库存代码。
没有在测试里复制剧情分支或计分规则。

GameBoundaryStubs.cs 替换游戏持有的数据、物品和库存 API、UI、原版 StoreReputation 与本地化表边界。
HospitalReputation 和 HospitalReputationTexts 使用实际生产代码，测试真实 perk 注册与本地化内容。
Dialogue 替身按本地反编译源码 `.tmp-decomp/Dialogue.cs` 的
SetStartAction、SetNextDialogue、ExecuteEndAction 语义推进。
选项驱动按 `.tmp-decomp/DialogUIManager.cs` 的 SelectChoice→ContinueConversation 路径，
先选菜单载体，再进入其next玩家对白并执行startAction，不能直接播放载体来代替原版跳转。
Conversation 驱动真实 StoryPlayer 创建的台词和 AddChoice 回调；
通过界面选项进入下一段，检查货物、现金、声望、合同日期和说话人。
Host测试验证卸载时取消播放、清除注册NPC队列、释放匹配的当前客户，以及不干扰其他客户；
主菜单无PlayerStore实例时检查不会经Instance懒创建存档。
退出存档回归直接编译并调用剧情层StoryMainMenuExitPatch、StoryQuitToMenuPatch，分别验证两个退出入口在切换存档/UI前清理活动播放器。

这些测试不加载 Unity，不验证游戏的菜单渲染、协程延时、真实存档序列化、
真实游戏中的声望 perk UI 渲染或游戏库存的拖放行为。替身中的边界函数只保留本测试所需行为，
不能把测试通过等同于游戏内实测通过。
