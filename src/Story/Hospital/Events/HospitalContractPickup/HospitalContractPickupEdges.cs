using System;
using System.Threading.Tasks;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupEdge : StoryEdge
    {
        internal HospitalContractPickupEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractSettlement result
                = HospitalContractTrade.TrySettle(
                    HospitalContractDefinition.Chemicals);

            HospitalContractState.MarkPickup(
                StoreStation.GetDayCounter());

            ReputationUIManager.Instance.Notify(
                "医院合同 "
                + (int)Math.Floor(result.Score)
                + " / "
                + HospitalContractScore.Target
                + " · 声望 "
                + (result.ReputationDelta >= 0 ? "+" : "")
                + result.ReputationDelta);

            if (result.ItemCount == 0)
            {
                player.SayNpc(HospitalContractPickupTexts.Zero1);
                player.SayNpc(HospitalContractPickupTexts.Zero2);
                player.SayNpc(HospitalContractPickupTexts.Zero3);
            }
            else if (result.Score < HospitalContractScore.Target)
            {
                player.SayNpc(HospitalContractPickupTexts.Short1);
                player.SayNpc(HospitalContractPickupTexts.Short2);
                player.SayNpc(HospitalContractPickupTexts.Short3);
                player.SayNpc(HospitalContractPickupTexts.Short4);
            }
            else if (result.HasHighPurity
                || result.Score >= HospitalContractScore.Target * 2d)
            {
                player.SayNpc(HospitalContractPickupTexts.Over1);
                player.SayNpc(HospitalContractPickupTexts.Over2);
                player.SayNpc(HospitalContractPickupTexts.Over3);
                player.SayNpc(HospitalContractPickupTexts.Over4);
            }
            else
            {
                player.SayNpc(HospitalContractPickupTexts.Met1);
                player.SayNpc(HospitalContractPickupTexts.Met2);
                player.SayNpc(HospitalContractPickupTexts.Met3);
            }

            return Task.CompletedTask;
        }
    }
}
