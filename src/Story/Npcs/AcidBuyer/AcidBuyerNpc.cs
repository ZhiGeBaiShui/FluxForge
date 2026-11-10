using FluxForge.Story.Portraits;

namespace FluxForge.Story.AcidBuyer
{
    /// <summary>
    /// 随机到访的酸/碱收货商。到访概率与展示柜、库存都无关。
    /// </summary>
    internal static class AcidBuyerNpc
    {
        private const string Identifier = "fluxforge_acid_buyer";
        private const string DisplayName = "薇拉";
        private const int VisitChancePercent = 100;
        private const string CheckedDayKey
            = "fluxforge.acidBuyer.checkedDay";

        internal static void Subscribe()
        {
            ModHook.OnShutterOpenedLate += Queue;
        }

        internal static void Unsubscribe()
        {
            ModHook.OnShutterOpenedLate -= Queue;
        }

        private static void Queue()
        {
            PlayerStore store = PlayerStore.Instance;
            int day = StoreStation.GetDayCounter();
            string checkedDay;
            if (store.modData.TryGetValue(CheckedDayKey, out checkedDay)
                && checkedDay == day.ToString())
            {
                return;
            }

            store.modData[CheckedDayKey] = day.ToString();

            if (!RNG.Roll(VisitChancePercent))
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

        private static StoreClient Create()
        {
            StoreClient client = new StoreClient
            {
                identifier = Identifier,
                displayName = DisplayName,
                spriteName = NpcPortraitRegistry.AcidBuyerSpriteKey,
                clientIntent = StoreClient.ClientIntent.DIALOGUE,
                dismissable = false,
                arrestable = false,
                OnArrival = StartStory,
            };

            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, "fluxforge");
            return client;
        }

        private static void StartStory()
        {
            foreach (NpcStoryAssembly assembly in
                AcidBuyerPlan.CreateStories())
            {
                if (!assembly.CanPlay())
                {
                    continue;
                }

                string npcName = PlayerStore.Instance.currentClientInstance
                    .GetClientBlueprint().displayName;

                StoryPlayer player = new StoryPlayer(
                    delegate { return npcName; },
                    OnStoryFinished);

                _ = player.Start(assembly);
                return;
            }
        }

        private static void OnStoryFinished()
        {
            PlayerStore.Instance.currentClientInstance
                .GetClientBlueprint().dismissable = true;

            DialogUIManager.Instance.CurrentClientLeave();
        }
    }
}
