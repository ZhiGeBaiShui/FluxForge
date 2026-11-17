using System.Globalization;

namespace FluxForge.Story.Hospital
{
    /// <summary>本次到访的累计交货；只在内存中存在，结束时统一结算。</summary>
    internal sealed class HospitalPickupSession
    {
        internal double Score { get; private set; }
        internal int ItemCount { get; private set; }
        internal int OrdinaryItemCount { get; private set; }
        internal bool IsTrustedShortDelivery
        {
            get
            {
                return Score < HospitalContractScore.Target
                    && HospitalReputation.Get() >= HospitalContractScore.ExternalExpertReputationThreshold;
            }
        }

        internal void BeginVisit()
        {
            Score = 0d;
            ItemCount = 0;
            OrdinaryItemCount = 0;
        }

        internal void Submit(HospitalContractInspection inspection)
        {
            HospitalContractTrade.Take(inspection);
            Add(inspection.Score, inspection.Qualified.Count);
            OrdinaryItemCount += inspection.Qualified.Count;
        }

        internal void Add(double score, int count)
        {
            Score += score;
            ItemCount += count;
            ReputationUIManager.Instance.Notify(string.Format(
                CultureInfo.InvariantCulture, ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Score),
                Score.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        internal void Finish(StorySequence player)
        {
            int payment = HospitalContractTrade.Settle(Score);
            HospitalContractState.MarkPickup(StoreStation.GetDayCounter());
            if (!HospitalHighestDemand.Completing)
            {
                player.SaySystem(string.Format(CultureInfo.InvariantCulture,
                    ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Total),
                    Score.ToString("0.###", CultureInfo.InvariantCulture), payment));
                if (!IsTrustedShortDelivery && ItemCount == 0)
                {
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroSettled));
                }
                else if (!IsTrustedShortDelivery && OrdinaryItemCount > 0)
                {
                    if (Score >= HospitalContractScore.Target * 2d)
                    {
                        player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.OverThanks));
                        player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Over3));
                    }
                    else
                    {
                        player.SayNpc(Score >= HospitalContractScore.Target
                            ? ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Met1) : ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortSettled));
                    }
                }
            }
            player.Insert(HospitalStoryPoints.PickupSettled);
            if (IsTrustedShortDelivery)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Busy1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Busy2));
            }
            else
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Farewell));
            }
        }
    }
}
