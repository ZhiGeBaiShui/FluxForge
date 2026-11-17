using FluxForge.Story;
using Xunit;

namespace FluxForge.Story.Tests
{
    public sealed class StoryHostLifecycleTests
    {
        private sealed class PendingAssembly : StoryAssembly
        {
            internal override StoryEdge Entry { get; }

            internal PendingAssembly(Action action)
            {
                Entry = new PendingEdge(action);
            }
        }

        private sealed class PendingEdge : StoryEdge
        {
            private readonly Action _action;

            internal PendingEdge(Action action) : base(null, null)
            {
                _action = action;
            }

            internal override void Execute(StorySequence sequence)
            {
                sequence.SayNpc("pending");
                sequence.Do(_action);
            }
        }

        public StoryHostLifecycleTests()
        {
            PlayerStore.Instance = new PlayerStore();
            DialogUIManager.Instance = new DialogUIManager();
            StoryNpcRuntime.Shutdown();
        }

        private static NpcStoryContent Content(StoryEventDefinition[] events = null)
        {
            NpcDefinition npc = new NpcDefinition("test", "NPC", "portrait", null, true);
            VisitRule rule = new VisitRule("test.visit", "test", 1, 1d, 0,
                VisitKind.Dialogue, context => true, null);
            return new NpcStoryContent(npc, new[] { rule }, events ?? Array.Empty<StoryEventDefinition>(), rule);
        }

        private static NpcStoryHost Host(NpcStoryContent content)
        {
            NpcStoryHost host = new NpcStoryHost();
            host.Add(content);
            host.Subscribe();
            return host;
        }

        [Fact]
        public void NativeFactoryAndNameLookupDoNotCreateSaveOrStartVisit()
        {
            NpcStoryContent content = Content();
            NpcStoryHost host = Host(content);
            PlayerStore.Instance = null;

            Assert.Equal("NPC", StoreClientListDict.GetClientDisplayName("test"));
            StoreClient client = StoreClientListDict.CreateStoreClient("test");

            Assert.Equal("test", client.identifier);
            Assert.Equal("portrait", client.spriteName);
            Assert.False(PlayerStore.HasInstance);
            host.Dispose();
            Assert.False(StoreClientListDict.storeClientDict.ContainsKey("test"));
        }

        [Fact]
        public void NativeFactoryBypassesSchedulingButDefersVisitEffectsUntilArrival()
        {
            int starts = 0;
            NpcDefinition npc = Content().Definition;
            VisitRule direct = new VisitRule("direct", "test", 1, 0d, 7,
                VisitKind.Sell, context => false, () => starts++);
            NpcStoryContent content = new NpcStoryContent(npc, new[] { direct },
                Array.Empty<StoryEventDefinition>(), direct);
            NpcStoryHost host = Host(content);
            ModHook.FireShutterOpened();
            Assert.Empty(PlayerStore.Instance.storeClientManager.clientStack);

            StoreClient client = StoreClientListDict.CreateStoreClient("test");
            Assert.Equal(StoreClient.ClientIntent.SELL, client.clientIntent);
            Assert.Equal(0, starts);
            PlayerStore.Instance.currentClientInstance = new StoreClientInstance(client);
            client.OnArrival();

            Assert.Equal(1, starts);
            Assert.True(client.dismissable);
            host.Dispose();
        }

        [Fact]
        public void NativeFactorySelectsCurrentStoryOnlyWhenClientArrives()
        {
            bool ready = false;
            StoryEventDefinition definition = new StoryEventDefinition(
                new StoryEvent("pending", "test", StoryTrigger.VisitStarted, 1, 0, null,
                    context => ready),
                new PendingAssembly(() => { }), StoryOutcome.Leave);
            NpcStoryHost host = Host(Content(new[] { definition }));
            StoreClient client = StoreClientListDict.CreateStoreClient("test");
            ready = true;
            PlayerStore.Instance.currentClientInstance = new StoreClientInstance(client);
            client.OnArrival();

            Assert.Equal("pending", DialogUIManager.Instance.Current.Text);
            Assert.False(client.dismissable);
            StoryNpcRuntime.Shutdown();
            host.Dispose();
        }

        [Fact]
        public void DisposeClearsNativeNameCacheAndKeepsOtherFactories()
        {
            StoreClientListDict.storeClientDict.Add("ordinary", () => new StoreClient());
            NpcStoryHost host = Host(Content());
            Assert.Equal("NPC", StoreClientListDict.GetClientDisplayName("test"));
            host.Dispose();
            Assert.True(StoreClientListDict.storeClientDict.ContainsKey("ordinary"));

            NpcStoryContent renamed = Content();
            NpcDefinition npc = new NpcDefinition("test", "Renamed", "portrait", null, true);
            NpcStoryHost next = Host(new NpcStoryContent(npc, renamed.VisitRules,
                renamed.EventDefinitions, renamed.DirectVisitRule));
            Assert.Equal("Renamed", StoreClientListDict.GetClientDisplayName("test"));
            next.Dispose();
            StoreClientListDict.storeClientDict.Remove("ordinary");
        }

        [Fact]
        public void DisposeRemovesRegisteredQueuedClientsAndKeepsOrdinaryClients()
        {
            NpcStoryHost host = Host(Content());
            StoreClient ordinary = new StoreClient { identifier = "ordinary", dismissable = false };
            PlayerStore.Instance.storeClientManager.clientStack.Add(ordinary);
            ModHook.FireShutterOpened();
            ModHook.FireGameLoaded();
            Assert.Equal(2, PlayerStore.Instance.storeClientManager.clientStack.Count);

            host.Dispose();

            Assert.Same(ordinary, Assert.Single(PlayerStore.Instance.storeClientManager.clientStack));
        }

        [Fact]
        public void ShutdownDismissesTheCurrentRegisteredClientWithoutCompletingItsStory()
        {
            int actions = 0;
            StoryEventDefinition definition = new StoryEventDefinition(
                new StoryEvent("pending", "test", StoryTrigger.VisitStarted, 1, 0, null, context => true),
                new PendingAssembly(() => actions++), StoryOutcome.Leave);
            NpcStoryHost host = Host(Content(new[] { definition }));
            ModHook.FireShutterOpened();
            StoreClient client = Assert.Single(PlayerStore.Instance.storeClientManager.clientStack);
            PlayerStore.Instance.storeClientManager.clientStack.Clear();
            PlayerStore.Instance.currentClientInstance = new StoreClientInstance(client);
            client.OnArrival();
            Assert.False(client.dismissable);

            StoryNpcRuntime.Shutdown();
            host.Dispose();

            Assert.Null(PlayerStore.Instance.currentClientInstance);
            Assert.Equal(1, PlayerStore.Instance.Dismissals);
            Assert.Null(DialogUIManager.Instance.Current);
            Assert.Equal(0, actions);
            Assert.Equal(0, new NpcState(Content().Definition).SeenCount("pending"));
        }

        [Fact]
        public void ShutdownLeavesTheCurrentOrdinaryClientAndItsDialogueAlone()
        {
            NpcStoryHost host = Host(Content());
            StoreClientInstance ordinary = new StoreClientInstance(new StoreClient { identifier = "ordinary" });
            Dialogue gameDialogue = new Dialogue();
            gameDialogue.SetText("NPC", "game dialogue");
            PlayerStore.Instance.currentClientInstance = ordinary;
            DialogUIManager.Instance.SetCurrentDialog(gameDialogue);

            StoryNpcRuntime.Shutdown();
            host.Dispose();

            Assert.Same(ordinary, PlayerStore.Instance.currentClientInstance);
            Assert.Same(gameDialogue, DialogUIManager.Instance.Current);
            Assert.Equal(0, PlayerStore.Instance.Dismissals);
        }

        [Fact]
        public void DisposeWithoutLoadedSaveDoesNotCreateOne()
        {
            NpcStoryHost host = Host(Content());
            PlayerStore.Instance = null;

            host.Dispose();

            Assert.False(PlayerStore.HasInstance);
        }

        [Fact]
        public void DisposeUnsubscribesBothVisitHooks()
        {
            NpcStoryHost host = Host(Content());
            host.Dispose();

            ModHook.FireGameLoaded();
            ModHook.FireShutterOpened();

            Assert.Empty(PlayerStore.Instance.storeClientManager.clientStack);
        }
    }
}
