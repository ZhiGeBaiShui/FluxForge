using FluxForge.Story;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class StoryRuntimeBugReproTests
    {
        private sealed class PendingAssembly : StoryAssembly
        {
            internal override StoryEdge Entry { get; } = new PendingEdge();
        }

        private sealed class PendingEdge : StoryEdge
        {
            internal PendingEdge() : base(null, null)
            {
            }

            internal override void Execute(StorySequence sequence)
            {
                sequence.SayNpc("Old save narrative still displaying");
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExitingSaveClearsTheOldPlayerBeforeChangingStoreAndUi(bool mainMenu)
        {
            DialogUIManager.Instance = new DialogUIManager();
            StoryNpcRuntime.Shutdown();
            PlayerStore.Instance = new PlayerStore();
            NpcDefinition npc = new NpcDefinition("old-save-npc", "NPC", "portrait", null, false);
            var content = new NpcStoryContent(npc, Array.Empty<VisitRule>(), Array.Empty<StoryEventDefinition>(), null);
            var context = new StoryContext(npc, new NpcState(npc), StoryTrigger.VisitStarted,
                VisitKind.Dialogue, 0, null);
            var playback = new StoryPlaybackContext(content, context, new HashSet<string>());
            StoryPlayer player = new StoryPlayer(() => "NPC", playback, () => { });
            StoryNpcRuntime.BeginStory(npc.Identifier, player);
            player.Start(new PendingAssembly());

            if (mainMenu)
            {
                StoryMainMenuExitPatch.Prefix();
            }
            else
            {
                StoryQuitToMenuPatch.Prefix();
            }
            Assert.Null(DialogUIManager.Instance.Current);

            // 原版 EscapeUIManager:107–111 重置存档并切换场景，不调用客户 OnDismiss。
            // 此处模拟换存档的数据和 UI 边界；不执行真实 Unity 场景加载。
            PlayerStore.Instance = new PlayerStore();
            PlayerStore.Instance.currentClientInstance = new StoreClientInstance(
                new StoreClient { identifier = "ordinary-new-save", dismissable = true });
            DialogUIManager.Instance = new DialogUIManager();
            int ordinaryEndActions = 0;
            Dialogue ordinary = new Dialogue();
            ordinary.SetText("CLIENT", "New save ordinary NPC is still typing");
            ordinary.endAction = () => ordinaryEndActions++;
            // 原版 StartDialogue → DisplayClientText 会记录 displayingUncompletedDialog。
            // 模拟普通行仍在打字、尚未触发 OnCurrentTextDisplayed 时；原版中断会执行其结束动作。
            DialogUIManager.Instance.StartDialogue(ordinary, forcedStart: true);

            StoryNpcRuntime.Shutdown();

            Assert.Equal(0, ordinaryEndActions);
            Assert.Same(ordinary, DialogUIManager.Instance.Current);
        }
    }
}
