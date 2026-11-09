using FluxForge.Story.Portraits;
using UnityEngine;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>
    /// 装置推销员的统筹层：负责注册、到店、首次剧情和首次库存。
    /// 两个剧情本身分别在 FlowEmitterArrival 与 FlowEmitterFarewell。
    /// </summary>
    internal static class FlowEmitterMerchantNpc
    {
        private const string Identifier = "fluxforge_flow_emitter_demo";
        private const string NextVisitDayKey
            = "fluxforge.flowEmitterDemo.nextVisitDay";
        private const int VisitIntervalDays = 7;
        private const string DisplayName = "奥雅";

        private static bool _storyRunning;

        internal static void Subscribe()
        {
            ModHook.OnGameLoadedLate += OnGameLoaded;
            ModHook.OnShutterOpenedLate += Queue;
        }

        internal static void Unsubscribe()
        {
            ModHook.OnGameLoadedLate -= OnGameLoaded;
            ModHook.OnShutterOpenedLate -= Queue;
            NpcPortraitRegistry.Shutdown();
        }

        private static void OnGameLoaded()
        {
            _storyRunning = false;
            NpcPortraitRegistry.Apply();
            Queue();
        }

        private static void Queue()
        {
            PlayerStore store = PlayerStore.Instance;

            if (!HasReachedVisitDay(store))
            {
                return;
            }

            if (store.storeClientManager.clientStack.Exists(
                    delegate(StoreClient client)
                    {
                        return client.identifier == Identifier;
                    }))
            {
                return;
            }

            StoreClientInstance current = store.currentClientInstance;
            if (current != null
                && current.GetClientBlueprint().identifier == Identifier)
            {
                return;
            }

            store.storeClientManager.AddNextClient(Create());
        }

        private static bool HasReachedVisitDay(PlayerStore store)
        {
            string raw;
            if (!store.modData.TryGetValue(NextVisitDayKey, out raw))
            {
                return true;
            }

            int nextDay = int.Parse(raw);

            return StoreStation.GetDayCounter() >= nextDay;
        }

        private static StoreClient Create()
        {
            StoreClient client = new StoreClient
            {
                identifier = Identifier,
                displayName = DisplayName,
                spriteName = NpcPortraitRegistry.SpriteKey,
                clientIntent = StoreClient.ClientIntent.SELL,
                dismissable = true,
                arrestable = false,
                mainDialogue = null,
                OnArrival = StartStory,
                tryInterceptDismiss = TryInterceptFirstDeparture,
            };

            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, "fluxforge");
            return client;
        }

        private static void StartStory()
        {
            PlayerStore store = PlayerStore.Instance;

            FlowEmitterMerchantProgress.IncrementVisitCount();

            store.modData[NextVisitDayKey]
                = (StoreStation.GetDayCounter() + VisitIntervalDays)
                    .ToString();

            PlayerStore.Instance.currentClientInstance
                .GetClientBlueprint().dismissable = false;

            if (!TryPlay(StoryPhase.Arrival)
                && !TryPlay(StoryPhase.SecondVisit)
                && !TryPlay(StoryPhase.GenericVisit))
            {
                OpenShop();
            }
        }

        private static bool TryPlay(StoryPhase phase)
        {
            if (_storyRunning)
            {
                return false;
            }

            foreach (NpcStoryAssembly assembly in
                FlowEmitterMerchantPlan.CreateStories())
            {
                if (assembly.Phase != phase || !assembly.CanPlay())
                {
                    continue;
                }

                _storyRunning = true;

                string npcName = GetCurrentClientName();

                StoryPlayer player = new StoryPlayer(
                    delegate { return npcName; },
                    delegate { OnStoryFinished(phase); });

                _ = player.Start(assembly);
                return true;
            }

            return false;
        }

        private static void OnStoryFinished(StoryPhase phase)
        {
            _storyRunning = false;

            if (phase == StoryPhase.Arrival)
            {
                FinishStory();
                return;
            }

            if (phase == StoryPhase.SecondVisit)
            {
                OpenShop();
                return;
            }

            if (phase == StoryPhase.GenericVisit)
            {
                OpenShop();
                return;
            }

            if (phase == StoryPhase.SoldOut)
            {
                DialogUIManager.Instance.CurrentClientLeave();
            }
        }

        private static void OpenShop()
        {
            StoreClient client = PlayerStore.Instance.currentClientInstance
                .GetClientBlueprint();

            client.dismissable = true;
            FlowEmitterMerchantStock.Place();
        }

        private static string GetCurrentClientName()
        {
            StoreClientInstance instance = PlayerStore.Instance.currentClientInstance;
            return instance.GetClientBlueprint().displayName;
        }

        private static void FinishStory()
        {
            PlayerStore store = PlayerStore.Instance;

            StoreClient client = store.currentClientInstance
                .GetClientBlueprint();

            client.dismissable = true;
        }

        private static bool TryInterceptFirstDeparture()
        {
            if (FlowEmitterMerchantProgress.HasSecondVisit)
            {
                return false;
            }

            return TryPlay(StoryPhase.SoldOut);
        }
    }
}


