using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>
    /// 流源发射机剧情的 NPC。
    /// 首次到店播放剧情并上破旧库存；之后到店跳过剧情，上正常库存。
    /// </summary>
    internal static class FlowEmitterDemoNpc
    {
        private const string Identifier = "fluxforge_flow_emitter_demo";
        private const string StorySeenKey = "fluxforge.flowEmitterDemo.storySeen";
        private const string DisplayName = "装置推销员";

        private static bool _firstStory;

        internal static void Subscribe()
        {
            ModHook.OnGameLoadedLate += Queue;
            ModHook.OnShutterOpenedLate += Queue;
        }

        internal static void Unsubscribe()
        {
            ModHook.OnGameLoadedLate -= Queue;
            ModHook.OnShutterOpenedLate -= Queue;
        }

        private static void Queue()
        {
            PlayerStore store = PlayerStore.Instance;

            if (store.storeClientManager.clientStack.Exists(
                    client => client.identifier == Identifier))
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
                spriteName = SpriteDict.GetRandomMerchantSpriteName(),
                clientIntent = StoreClient.ClientIntent.SELL,
                dismissable = true,
                arrestable = false,
                mainDialogue = null,
                OnArrival = StartStory,
            };

            client.CompleteClientCreation();
            ModHelper.SetClientSource(client, "fluxforge");
            return client;
        }

        private static void StartStory()
        {
            PlayerStore store = PlayerStore.Instance;
            _firstStory = !store.modData.ContainsKey(StorySeenKey);

            PlayerStore.Instance.currentClientInstance
                .GetClientBlueprint().dismissable = false;

            if (!_firstStory)
            {
                OpenShop();
                return;
            }

            string npcName = GetCurrentClientName();

            StoryPlayer player = new StoryPlayer(
                EmporiumEntry.Instance.frontInvinvElement,
                () => npcName,
                FinishStory);

            _ = player.Start(new FlowEmitterDemoAssembly().Entry);
        }

        private static void OpenShop()
        {
            PlayerStore.Instance.currentClientInstance
                .GetClientBlueprint().dismissable = true;

            FlowEmitterDemoStock.Place(_firstStory);
        }

        private static string GetCurrentClientName()
        {
            StoreClientInstance instance = PlayerStore.Instance.currentClientInstance;
            return instance.GetClientBlueprint().displayName;
        }

        private static void FinishStory()
        {
            PlayerStore store = PlayerStore.Instance;
            store.modData[StorySeenKey] = "1";

            FlowEmitterDemoStock.Place(_firstStory);

            store.currentClientInstance
                .GetClientBlueprint().dismissable = true;
        }
    }
}
