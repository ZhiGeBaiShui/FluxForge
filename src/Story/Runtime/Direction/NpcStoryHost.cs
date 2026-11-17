using System;
using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>
    /// 通用 NPC 到访控制器：决定谁来访、创建访客、选择剧情、执行结局。
    /// 具体 NPC 的到访规则、事件条件和内容通过 NpcStoryContent 注册。
    /// </summary>
    internal sealed class NpcStoryHost
    {
        private readonly List<NpcStoryContent> _contents
            = new List<NpcStoryContent>();
        private readonly Action _queueOnShutter;

        internal NpcStoryHost()
        {
            _queueOnShutter = delegate { QueueAll(false); };
        }

        internal void Add(NpcStoryContent content)
        {
            StoreClientListDict.storeClientDict.Add(
                content.Definition.Identifier,
                delegate { return Create(content, new VisitDecision(content.DirectVisitRule)); });
            _contents.Add(content);
        }

        internal void Subscribe()
        {
            ModHook.OnGameLoadedLate += OnGameLoaded;
            ModHook.OnShutterOpenedLate += _queueOnShutter;
        }

        internal void Dispose()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            ModHook.OnShutterOpenedLate -= _queueOnShutter;
            RemoveClients();
            for (int i = 0; i < _contents.Count; i++)
            {
                StoreClientListDict.storeClientDict.Remove(
                    _contents[i].Definition.Identifier);
            }
            StoreClientListDict.ClearDisplayNameCache();
        }

        private void RemoveClients()
        {
            // Instance 会懒建存档；菜单中卸载时不能因清理而创建存档。
            if (!PlayerStore.HasInstance)
            {
                return;
            }

            PlayerStore store = PlayerStore.Instance;
            store.storeClientManager.clientStack.RemoveAll(IsRegisteredClient);
            StoreClientInstance current = store.currentClientInstance;
            if (current != null && IsRegisteredClient(current.GetClientBlueprint()))
            {
                store.DismissCurrentClient(callNextWhenPossible: false);
            }
        }

        private bool IsRegisteredClient(StoreClient client)
        {
            for (int i = 0; i < _contents.Count; i++)
            {
                if (_contents[i].Definition.Identifier == client.identifier)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnGameLoaded()
        {
            QueueAll(true);
        }

        private void QueueAll(bool gameLoad)
        {
            for (int i = 0; i < _contents.Count; i++)
            {
                NpcStoryContent content = _contents[i];
                if (gameLoad && !content.Definition.QueueOnGameLoad)
                {
                    continue;
                }

                Queue(content);
            }
        }

        private static void Queue(NpcStoryContent content)
        {
            PlayerStore store = PlayerStore.Instance;
            NpcState state = new NpcState(content.Definition);
            int day = StoreStation.GetDayCounter();

            if (IsQueuedOrCurrent(store, content.Definition.Identifier))
            {
                return;
            }

            VisitContext context = new VisitContext(
                content.Definition,
                state,
                day);

            VisitDecision decision = VisitRuleSelector.Select(
                content.VisitRules,
                context,
                delegate { return RNG.GetRandomFloat(0f, 1f); });

            if (decision == null)
            {
                return;
            }

            store.storeClientManager.AddNextClient(
                Create(content, decision));
        }

        private static bool IsQueuedOrCurrent(
            PlayerStore store,
            string identifier)
        {
            if (store.storeClientManager.clientStack.Exists(
                    delegate(StoreClient client)
                    {
                        return client.identifier == identifier;
                    }))
            {
                return true;
            }

            StoreClientInstance current = store.currentClientInstance;
            return current != null
                && current.GetClientBlueprint().identifier == identifier;
        }

        private static StoreClient Create(
            NpcStoryContent content,
            VisitDecision decision)
        {
            StoreClient client = new StoreClient
            {
                identifier = content.Definition.Identifier,
                displayName = content.Definition.DisplayName,
                spriteName = content.Definition.SpriteKey,
                clientIntent = IntentFor(decision.Kind),
                dismissable = false,
                arrestable = false,
                mainDialogue = null,
                OnArrival = delegate { StartVisit(content, decision); },
                OnDismiss = delegate
                {
                    StoryNpcRuntime.CancelForClient(
                        content.Definition.Identifier);
                },
            };

            if (!string.IsNullOrEmpty(content.Definition.FactionId))
            {
                client.clientFaction = content.Definition.FactionId;
            }

            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, "fluxforge");
            return client;
        }

        private static StoreClient.ClientIntent IntentFor(VisitKind kind)
        {
            if (kind == VisitKind.Sell)
            {
                return StoreClient.ClientIntent.SELL;
            }

            if (kind == VisitKind.Buy)
            {
                return StoreClient.ClientIntent.BUY;
            }

            if (kind == VisitKind.SellBuy)
            {
                return StoreClient.ClientIntent.SELLNBUY;
            }

            return StoreClient.ClientIntent.DIALOGUE;
        }

        private static void StartVisit(
            NpcStoryContent content,
            VisitDecision decision)
        {
            PlayerStore store = PlayerStore.Instance;
            StoreClient client = store.currentClientInstance
                .GetClientBlueprint();
            NpcState state = new NpcState(content.Definition);
            int day = StoreStation.GetDayCounter();
            StoryNpcRuntime.ClearTradeCompleted();

            if (decision.Rule.OnVisitStarted != null)
            {
                decision.Rule.OnVisitStarted();
            }
            else
            {
                state.IncrementVisitCount();
            }

            state.SetLastVisitDay(day);

            StoryContext context = new StoryContext(
                content.Definition,
                state,
                StoryTrigger.VisitStarted,
                decision.Kind,
                day,
                null);

            HashSet<string> consumedGroups = new HashSet<string>();

            StoryEvent selected = StoryEventSelector.Select(
                content.Events,
                context,
                consumedGroups);

            if (selected == null)
            {
                ApplyOutcome(
                    client,
                    decision.Kind,
                    StoryOutcome.Default);
                return;
            }

            if (!string.IsNullOrEmpty(selected.ExclusionGroup))
            {
                consumedGroups.Add(selected.ExclusionGroup);
            }

            StoryEventDefinition definition = content.FindEvent(selected.Id);
            client.dismissable = false;

            StoryPlaybackContext playback = new StoryPlaybackContext(
                content, context, consumedGroups);
            NpcVisitAssembly visit = new NpcVisitAssembly(playback, definition);
            StoryPlayer player = new StoryPlayer(
                delegate { return client.displayName; }, playback,
                delegate
                {
                    StoryNpcRuntime.EndStory();
                    ApplyOutcome(client, decision.Kind, visit.Outcome);
                });
            StoryNpcRuntime.BeginStory(client.identifier, player);
            player.Start(visit);
        }

        private static void ApplyOutcome(
            StoreClient client,
            VisitKind kind,
            StoryOutcome outcome)
        {
            client.dismissable = true;

            StoryOutcome resolved = outcome == StoryOutcome.Default
                ? DefaultOutcome(kind)
                : outcome;

            if (resolved == StoryOutcome.Leave)
            {
                DialogUIManager.Instance.CurrentClientLeave();
            }
        }

        private static StoryOutcome DefaultOutcome(VisitKind kind)
        {
            return kind == VisitKind.Dialogue
                ? StoryOutcome.Leave
                : StoryOutcome.OpenShop;
        }
    }
}
