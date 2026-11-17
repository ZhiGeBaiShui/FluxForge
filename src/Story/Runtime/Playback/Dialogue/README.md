> 当前状态索引：2026-11-16，见[项目状态](../../../../../docs/current-project-state.md)。自动测试结果及待执行清理见维护记录。

# Dialogue

StoryDialogueAdapter：创建剧情行，并把玩家选项接入原版Dialogue。
选项是菜单载体→玩家对白两层，依据原版SelectChoice先选中载体、ContinueConversation再进入其next的语义。
真正玩家对白的startAction才执行选项回调；菜单载体不作为对白显示。玩家行使用原版PLAYER标识。
StoryDialogueBridge：不可见续接节点，在原版startAction阶段替换currentDialog，让子图返回/动态插入不显示空行。
DialoguePacing：仅为自定义行覆盖停顿时间。

桥的必要性见本地反编译DialogUIManager.OnCurrentTextDisplayed与ContinueConversation；
取消动作的必要性见PlayerStore.EndNight→DismissCurrentClient→OnDismiss。
