using System;
using System.Collections.Generic;
using FluxForge.Story.Portraits;

namespace FluxForge.Story
{
    /// <summary>
    /// 通用 NPC 到访控制器：决定谁来访、创建访客、选择剧情、执行结局。
    /// 新增剧情只需注册 StoryEvent，不修改这里。
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
            NpcPortraitRegistry.Shutdown();
        }

        private void OnGameLoaded()
        {
            NpcPortraitRegistry.Apply();
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

            NpcVisitSession session = new NpcVisitSession(
                content,
                decision,
                context);

            StoryEvent selected = StoryEventSelector.Select(
                content.Events,
                context,
                session.ConsumedGroups);

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
                session.ConsumedGroups.Add(selected.ExclusionGroup);
            }

            StoryEventDefinition definition = content.FindEvent(selected.Id);
            client.dismissable = false;

            StoryPlayer player = new StoryPlayer(
                delegate { return client.displayName; },
                delegate { OnStoryFinished(session, definition, client); });

            StoryNpcRuntime.BeginStory(client.identifier, player);
            _ = player.Start(definition.Content);
        }

        private static void OnStoryFinished(
            NpcVisitSession session,
            StoryEventDefinition definition,
            StoreClient client)
        {
            StoryNpcRuntime.EndStory(client.identifier);
            session.Context.State.MarkSeen(definition.Event.Id);

            StoryTradeResult trade = StoryNpcRuntime.TakeTradeCompleted();
            if (trade != null)
            {
                StoryContext tradeContext = new StoryContext(
                    session.Context.Definition,
                    session.Context.State,
                    StoryTrigger.TradeCompleted,
                    session.Context.VisitKind,
                    session.Context.Day,
                    trade);

                StoryEvent tradeEvent = StoryEventSelector.Select(
                    session.Content.Events,
                    tradeContext,
                    session.ConsumedGroups);

                if (tradeEvent != null)
                {
                    if (!string.IsNullOrEmpty(tradeEvent.ExclusionGroup))
                    {
                        session.ConsumedGroups.Add(tradeEvent.ExclusionGroup);
                    }

                    StoryEventDefinition tradeDefinition
                        = session.Content.FindEvent(tradeEvent.Id);

                    StoryPlayer tradePlayer = new StoryPlayer(
                        delegate { return client.displayName; },
                        delegate
                        {
                            StoryNpcRuntime.EndStory(client.identifier);
                            session.Context.State.MarkSeen(
                                tradeDefinition.Event.Id);
                            RunEnding(session, definition, client);
                        });

                    StoryNpcRuntime.BeginStory(
                        client.identifier,
                        tradePlayer);
                    _ = tradePlayer.Start(tradeDefinition.Content);
                    return;
                }
            }

            RunEnding(session, definition, client);
        }

        private static void RunEnding(
            NpcVisitSession session,
            StoryEventDefinition definition,
            StoreClient client)
        {
            StoryContext endingContext = new StoryContext(
                session.Context.Definition,
                session.Context.State,
                StoryTrigger.VisitEnding,
                session.Context.VisitKind,
                session.Context.Day,
                null);

            StoryEvent ending = StoryEventSelector.Select(
                session.Content.Events,
                endingContext,
                session.ConsumedGroups);

            if (ending == null)
            {
                ApplyOutcome(
                    client,
                    session.Decision.Kind,
                    definition.Outcome);
                return;
            }

            if (!string.IsNullOrEmpty(ending.ExclusionGroup))
            {
                session.ConsumedGroups.Add(ending.ExclusionGroup);
            }

            StoryEventDefinition endingDefinition
                = session.Content.FindEvent(ending.Id);

            StoryPlayer player = new StoryPlayer(
                delegate { return client.displayName; },
                delegate
                {
                    StoryNpcRuntime.EndStory(client.identifier);
                    session.Context.State.MarkSeen(endingDefinition.Event.Id);
                    ApplyOutcome(
                        client,
                        session.Decision.Kind,
                        endingDefinition.Outcome == StoryOutcome.Default
                            ? definition.Outcome
                            : endingDefinition.Outcome);
                });

            StoryNpcRuntime.BeginStory(client.identifier, player);
            _ = player.Start(endingDefinition.Content);
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
