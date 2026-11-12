using System;
using System.Threading.Tasks;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupGreetingEdge : StoryEdge
    {
        internal HospitalContractPickupGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractPickupTexts.Greeting);
            return Task.CompletedTask;
        }
    }

    /// <summary>玩家确认放货后才扫描后柜台并结算。</summary>
    internal sealed class HospitalContractPickupConfirmEdge : StoryEdge
    {
        internal HospitalContractPickupConfirmEdge()
            : base(HospitalContractPickupTexts.ConfirmOption, "question")
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
            }
            else if (result.HasHighPurity
                || result.Score >= HospitalContractScore.Target * 2d)
            {
                player.SayNpc(HospitalContractPickupTexts.Over1);
                player.SayNpc(HospitalContractPickupTexts.Over2);
                player.SayNpc(HospitalContractPickupTexts.Over3);
            }
            else
            {
                player.SayNpc(HospitalContractPickupTexts.Met1);
                player.SayNpc(HospitalContractPickupTexts.Met2);
            }

            return Task.CompletedTask;
        }
    }
}
