namespace FluxForge.Story.Hospital
{
    /// <summary>在原版每日顾客生成阶段追加特殊采购客人。</summary>
    internal sealed class HospitalProcurement
    {
        private readonly string[] _clientIds;

        internal HospitalProcurement(string[] clientIds)
        {
            _clientIds = clientIds;
        }

        internal void Subscribe()
        {
            ModHook.OnGenerateCustomerLate += QueueClient;
        }

        internal void Dispose()
        {
            ModHook.OnGenerateCustomerLate -= QueueClient;
            // 原版 PlayerStore.Instance 会懒建存档，菜单中注销时不能读取它。
            if (!PlayerStore.HasInstance)
            {
                return;
            }

            PlayerStore store = PlayerStore.Instance;
            store.storeClientManager.clientStack.RemoveAll(IsProcurementClient);
            StoreClientInstance current = store.currentClientInstance;
            if (current != null && IsProcurementClient(current.GetClientBlueprint()))
            {
                store.DismissCurrentClient(callNextWhenPossible: false);
            }
        }

        private bool IsProcurementClient(StoreClient client)
        {
            for (int i = 0; i < _clientIds.Length; i++)
            {
                if (_clientIds[i] == client.identifier)
                {
                    return true;
                }
            }
            return false;
        }

        private void QueueClient(StoreClientManager manager)
        {
            if (StoreReputation.IsPerkUnlocked(
                    HospitalReputationTexts.SpecialProcurementPerkId)
                && RNG.Roll(10))
            {
                int index = RNG.GetRandomInt(0, _clientIds.Length - 1);
                manager.AddClient(StoreClientListDict.CreateStoreClient(_clientIds[index]));
            }
        }
    }
}
