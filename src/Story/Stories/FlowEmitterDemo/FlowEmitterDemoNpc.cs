using FluxForge.Mod;
using UnityEngine;

namespace FluxForge.Story.FlowEmitterDemo
{
    /// <summary>
    /// 流源发射机剧情的 NPC。
    /// 负责创建 StoreClient、在到店时启动剧情、退出时移除自己。
    /// </summary>
    internal static class FlowEmitterDemoNpc
    {
        private const string Identifier = "fluxforge_flow_emitter_demo";
        private const string CompletedKey = "fluxforge.flowEmitterDemo.completed";
        private const string DisplayName = "装置推销员";

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

            if (store.modData.ContainsKey(CompletedKey))
            {
                return;
            }

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
                clientIntent = StoreClient.ClientIntent.DIALOGUE,
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
            string npcName = GetCurrentClientName();

            StoryPlayer player = new StoryPlayer(
                EmporiumEntry.Instance.frontInvinvElement,
                () => npcName,
                FinishStory);

            _ = player.Start(new FlowEmitterDemoAssembly().Entry);
        }

        private static string GetCurrentClientName()
        {
            StoreClientInstance instance = PlayerStore.Instance.currentClientInstance;
            return instance.GetClientBlueprint().displayName;
        }

        private static void FinishStory()
        {
            PlayerStore store = PlayerStore.Instance;
            store.modData[CompletedKey] = "1";
            store.DismissCurrentClient(callNextWhenPossible: false);
        }
    }
}
