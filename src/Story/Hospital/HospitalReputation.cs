using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院声望：只在合同签订后注册，复用原版 StoreReputation。</summary>
    internal static class HospitalReputation
    {
        internal const string FactionId = "FACTION_FLUXFORGE_HOSPITAL";
        internal const string DisplayName = "上层区中心医院";

        internal static bool IsRegistered
        {
            get
            {
                return StoreReputation.GetStoreReputation(FactionId) != null;
            }
        }

        internal static void EnsureFaction()
        {
            if (IsRegistered)
            {
                return;
            }

            StoreReputation reputation = new StoreReputation
            {
                factionId = FactionId,
                factionDisplay = DisplayName,
                factionColor1 = "42a2aa",
                factionColor2 = "743d70",
                factionColor3 = "ac7b9a",
            };

            PlayerStore.Instance.storeReputations.Add(reputation);
            StoreReputation.UpdateReputation();
        }

        internal static int Get()
        {
            StoreReputation reputation
                = StoreReputation.GetStoreReputation(FactionId);
            return reputation == null ? 0 : reputation.GetReputation();
        }

        internal static void Add(double amount)
        {
            StoreReputation reputation
                = StoreReputation.GetStoreReputation(FactionId);
            reputation.ModReputation(amount);
            ReputationUIManager.Instance.NotifyReputation(
                FactionId,
                (int)System.Math.Round(amount));
        }

        /// <summary>预留给后续医院 NPC 的普通交易接入；当前无调用方。</summary>
        internal static void AddTrade(double cashValue)
        {
            Add(cashValue / 400d);
        }
    }
}
