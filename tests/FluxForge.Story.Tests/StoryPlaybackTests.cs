using FluxForge.Story;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class StoryPlaybackTests
    {
        private sealed class Content : StoryAssembly
        {
            internal override StoryEdge Entry { get; }
            private readonly Action _complete;
            internal Content(Action<StorySequence> register, Action complete = null)
            {
                Entry = new Edge(null, register);
                _complete = complete ?? (() => { });
            }
            internal Content(StoryEdge entry) { Entry = entry; _complete = () => { }; }
            internal override void Complete() { _complete(); }
        }
        private sealed class Edge : StoryEdge
        {
            private readonly Action<StorySequence> _register;
            internal Edge(string label, Action<StorySequence> register) : base(label, "question") { _register = register; }
            internal override void Execute(StorySequence sequence) { _register(sequence); }
        }
        private sealed class Run
        {
            internal readonly StoryPlayer Player;
            internal readonly List<string> Lines = new();
            internal readonly StoryPlaybackContext Playback;
            internal Dialogue Last;
            internal int Exits;
            internal Run(StoryAssembly root, StoryEventDefinition[] definitions = null, bool advance = true)
            {
                NpcDefinition npc = new NpcDefinition("test", "NPC", "portrait", null, false);
                NpcStoryContent content = new NpcStoryContent(npc, Array.Empty<VisitRule>(), definitions ?? Array.Empty<StoryEventDefinition>(), null);
                Playback = new StoryPlaybackContext(content,
                    new StoryContext(npc, new NpcState(npc), StoryTrigger.VisitStarted, VisitKind.Dialogue, 0, null),
                    new HashSet<string>());
                Player = new StoryPlayer(() => "NPC", Playback, () => Exits++);
                Player.Start(root);
                if (advance) Pump(DialogUIManager.Instance.Current);
            }
            internal void Pump(Dialogue line)
            {
                while (line != null)
                {
                    DialogUIManager.Instance.SetCurrentDialog(line);
                    line.StartAction?.Invoke();
                    line = DialogUIManager.Instance.GetCurrentDialog();
                    if (line == null) break;
                    Assert.False(string.IsNullOrEmpty(line.Text));
                    Lines.Add(line.Text);
                    if (line.Choices.Count == 0) Assert.NotNull(line.Next);
                    line.ExecuteEndAction();
                    Last = line;
                    line = line.Next;
                }
            }
            internal void Choose(string text)
            {
                // 对齐原版 SelectChoice → ContinueConversation：菜单载体本身不显示为对白。
                Dialogue choice = Assert.Single(Last.Choices.Where(c => c.Text == text)).Next;
                Pump(choice.Next);
            }
        }
        public StoryPlaybackTests()
        {
            PlayerStore.Instance = new PlayerStore();
            DialogUIManager.Instance = new DialogUIManager();
        }
        private static StoryEventDefinition Fragment(string id, string point, int priority,
            StoryAssembly content, Func<StoryContext, bool> condition, int times = 0, string group = null)
        {
            return new StoryEventDefinition(new StoryEvent(id, "test", point, priority, times, group, condition), content, StoryOutcome.Default);
        }

        [Fact]
        public void ExplicitPlayerLinesUseTheNativePlayerSpeakerMarker()
        {
            Run run = new Run(new Content(s => s.SayPlayer("player line")), advance: false);
            Assert.Equal("PLAYER", DialogUIManager.Instance.Current.Speaker);
            run.Pump(DialogUIManager.Instance.Current);
            Assert.Equal(new[] { "player line" }, run.Lines);
            Assert.Equal(1, run.Exits);
        }

        [Fact]
        public void NestedCallsReturnWithoutReplayingParentOrLeavingVisit()
        {
            int actions = 0, completed = 0;
            var grand = new Content(s => s.SayNpc("grand"), () => completed++);
            var child = new Content(s => { s.SayNpc("child"); s.Call(grand); s.SayNpc("child-return"); }, () => completed++);
            var root = new Content(s => { s.SayNpc("before"); s.Do(() => actions++); s.Call(child); s.SayNpc("after"); }, () => completed++);
            Run run = new Run(root);
            Assert.Equal(new[] { "before", "child", "grand", "child-return", "after" }, run.Lines);
            Assert.Equal(1, actions);
            Assert.Equal(3, completed);
            Assert.Equal(1, run.Exits);
            Assert.Equal(1, DialogUIManager.Instance.Starts);
        }

        [Fact]
        public void ChildChoiceReturnsToTheSameParentContinuation()
        {
            StoryNode options = new StoryNode();
            Edge entry = new Edge(null, s => s.SayNpc("child-question"));
            entry.Connect(null, options);
            new Edge("yes", s => s.SayNpc("child-answer")).Connect(options, null);
            var root = new Content(s => { s.SayNpc("before"); s.Call(new Content(entry)); s.SayNpc("after"); });
            Run run = new Run(root);
            Assert.Equal(0, run.Exits);
            Assert.Equal("child-question", run.Last.Text);
            run.Choose("yes");
            Assert.Equal(new[] { "before", "child-question", "yes", "child-answer", "after" }, run.Lines);
            Assert.Equal("after", run.Last.Text);
            Assert.Equal(1, run.Exits);
            Assert.Equal(1, DialogUIManager.Instance.Starts);
        }

        [Fact]
        public void InsertionsSeePrecedingActionsAndEachOtherInPriorityOrder()
        {
            var definitions = new[]
            {
                Fragment("second", "point", 10, new Content(s => s.SayNpc("second")), c => c.HasFlag("child-done")),
                Fragment("first", "point", 20, new Content(s => { s.SayNpc("first"); s.Do(() => PlayerStore.Instance.modData["child-done"] = "1"); }), c => c.HasFlag("ready"))
            };
            Run run = new Run(new Content(s => { s.SayNpc("before"); s.Do(() => PlayerStore.Instance.modData["ready"] = "1"); s.Insert("point"); s.SayNpc("after"); }), definitions);
            Assert.Equal(new[] { "before", "first", "second", "after" }, run.Lines);
            Assert.Equal(1, run.Playback.Visit.State.SeenCount("first"));
            Assert.Equal(1, run.Playback.Visit.State.SeenCount("second"));
        }

        [Fact]
        public void InsertionCountsAndExclusionGroupsReuseExistingSelectorRules()
        {
            var definitions = new[]
            {
                Fragment("one", "point", 30, new Content(s => s.SayNpc("one")), c => true, 1),
                Fragment("winner", "point", 20, new Content(s => s.SayNpc("winner")), c => true, group: "group"),
                Fragment("loser", "point", 10, new Content(s => s.SayNpc("loser")), c => true, group: "group"),
                Fragment("wrong-point", "other", 99, new Content(s => s.SayNpc("wrong")), c => true)
            };
            Run run = new Run(new Content(s => { s.Insert("point"); s.Insert("point"); s.SayNpc("after"); }), definitions);
            Assert.Equal(new[] { "one", "winner", "after" }, run.Lines);
        }

        [Fact]
        public void InsertionCandidatesKeepRegistrationOrderWithinEqualPriority()
        {
            StoryEventDefinition[] definitions = new[]
            {
                Fragment("middle-first", "point", 10, new Content(s => s.SayNpc("middle-first")), c => true),
                Fragment("low", "point", 0, new Content(s => s.SayNpc("low")), c => true),
                Fragment("high", "point", 20, new Content(s => s.SayNpc("high")), c => true),
                Fragment("middle-second", "point", 10, new Content(s => s.SayNpc("middle-second")), c => true)
            };
            Run run = new Run(new Content(s => s.Insert("point")), definitions);
            Assert.Equal(new[] { "high", "middle-first", "middle-second", "low" }, run.Lines);
            Assert.Equal(1, run.Exits);
        }

        [Fact]
        public void RegisteredCallCompletesOnceBeforeTheNextInsertionChecksItsLimit()
        {
            int completed = 0;
            var definition = Fragment("once", "point", 1,
                new Content(s => s.SayNpc("once"), () => completed++), c => true, 1);
            var npc = new NpcDefinition("test", "NPC", "portrait", null, false);
            var context = new StoryContext(npc, new NpcState(npc), StoryTrigger.VisitStarted,
                VisitKind.Dialogue, 0, null);
            var root = new Content(s =>
            {
                s.Call(definition, context);
                s.Insert("point");
                s.SayNpc("after");
            });
            Run run = new Run(root, new[] { definition });
            Assert.Equal(new[] { "once", "after" }, run.Lines);
            Assert.Equal(1, completed);
            Assert.Equal(1, run.Playback.Visit.State.SeenCount("once"));
            Assert.Equal(1, run.Exits);
        }

        [Fact]
        public void NoMatchingInsertionContinuesWithoutBlankDialogue()
        {
            Run run = new Run(new Content(s => { s.SayNpc("before"); s.Insert("empty"); s.SayNpc("after"); }));
            Assert.Equal(new[] { "before", "after" }, run.Lines);
        }

        [Fact]
        public void MultipleActionsOnOneLineExecuteInAuthorOrder()
        {
            List<int> order = new();
            new Run(new Content(s => { s.SayNpc("line"); s.Do(() => order.Add(1)); s.Do(() => order.Add(2)); }));
            Assert.Equal(new[] { 1, 2 }, order);
        }

        [Fact]
        public void CancellationSkipsPendingActionsChildCompletionAndParentContinuation()
        {
            int actions = 0, completed = 0;
            var child = new Content(s => { s.SayNpc("child"); s.Do(() => actions++); }, () => completed++);
            Run run = new Run(new Content(s => { s.Call(child); s.SayNpc("after"); }, () => completed++), advance: false);
            run.Player.Cancel();
            Assert.Equal(0, actions);
            Assert.Equal(0, completed);
            Assert.Equal(0, run.Exits);
            Assert.Null(DialogUIManager.Instance.Current);
        }

        [Fact]
        public void ShutdownCancelsTheActiveStoryAndDiscardsPendingTrade()
        {
            int actions = 0;
            Run run = new Run(new Content(s =>
            {
                s.SayNpc("pending");
                s.Do(() => actions++);
            }), advance: false);
            StoryNpcRuntime.BeginStory("test", run.Player);
            StoryNpcRuntime.QueueTradeCompleted(new StoryTradeResult(true, 100, "acid"));
            StoryNpcRuntime.Shutdown();
            Assert.Equal(0, actions);
            Assert.Equal(0, run.Exits);
            Assert.Null(DialogUIManager.Instance.Current);
            Assert.Null(StoryNpcRuntime.TakeTradeCompleted());
            StoryNpcRuntime.CancelForClient("test");
        }

        [Fact]
        public void ShutdownWithoutAnActiveStoryLeavesTheGameDialogueAlone()
        {
            Dialogue gameDialogue = new Dialogue();
            gameDialogue.SetText("NPC", "game dialogue");
            DialogUIManager.Instance.SetCurrentDialog(gameDialogue);
            StoryNpcRuntime.Shutdown();
            Assert.Same(gameDialogue, DialogUIManager.Instance.Current);
        }

        [Fact]
        public void VisitMainTradeAndEndingShareOnePlayerAndOneUiStart()
        {
            NpcDefinition npc = new NpcDefinition("test", "NPC", "portrait", null, false);
            var main = new StoryEventDefinition(new StoryEvent("main", "test", StoryTrigger.VisitStarted, 10, 0, null, c => true),
                new Content(s => { s.SayNpc("main"); s.Do(() => StoryNpcRuntime.QueueTradeCompleted(new StoryTradeResult(true, 100, "acid"))); }), StoryOutcome.Leave);
            var trade = new StoryEventDefinition(new StoryEvent("trade", "test", StoryTrigger.TradeCompleted, 10, 0, null, c => c.TradeResult.Success),
                new Content(s => { s.SayNpc("trade"); s.Insert("trade-point"); }), StoryOutcome.Leave);
            var ending = new StoryEventDefinition(new StoryEvent("ending", "test", StoryTrigger.VisitEnding, 10, 0, null, c => true),
                new Content(s => s.SayNpc("ending")), StoryOutcome.OpenShop);
            var content = new NpcStoryContent(npc, Array.Empty<VisitRule>(), new[] { main, trade, ending, Fragment("trade-extra", "trade-point", 1, new Content(s => s.SayNpc("trade-extra")), c => c.TradeResult.Success) }, null);
            var state = new NpcState(npc);
            var playback = new StoryPlaybackContext(content, new StoryContext(npc, state, StoryTrigger.VisitStarted, VisitKind.Dialogue, 0, null), new HashSet<string>());
            var visit = new NpcVisitAssembly(playback, main);
            int exits = 0;
            StoryPlayer player = new StoryPlayer(() => "NPC", playback, () => exits++);
            player.Start(visit);
            var lines = new List<string>();
            Dialogue line = DialogUIManager.Instance.Current;
            while (line != null)
            {
                DialogUIManager.Instance.SetCurrentDialog(line);
                line.StartAction?.Invoke();
                line = DialogUIManager.Instance.GetCurrentDialog();
                if (line == null) break;
                Assert.False(string.IsNullOrEmpty(line.Text));
                lines.Add(line.Text);
                line.ExecuteEndAction();
                line = line.Next;
            }
            Assert.Equal(new[] { "main", "trade", "trade-extra", "ending" }, lines);
            Assert.Equal(1, DialogUIManager.Instance.Starts);
            Assert.Equal(1, exits);
            Assert.Equal(StoryOutcome.OpenShop, visit.Outcome);
            Assert.Equal(1, state.SeenCount("main"));
            Assert.Equal(1, state.SeenCount("trade"));
            Assert.Equal(1, state.SeenCount("ending"));
        }
    }
}
