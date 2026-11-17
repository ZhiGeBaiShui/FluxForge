using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>到期送料：无玩家选项；问候行结束后发放原料、推进日期，再检查插入剧情。</summary>
    internal sealed class HospitalContractMaterialEdge : StoryEdge
    {
        internal HospitalContractMaterialEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            Append(player);
        }

        internal static void Append(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMaterialTexts.Greeting));
            player.Do(delegate
            {
                HospitalContractStock.GiveWeekly(
                    HospitalContractStock.WeeklyCount());
                if (HospitalSynaprestState.Completed)
                {
                    HospitalSynaprestStock.GiveWeekly();
                }
                HospitalContractState.MarkMaterial(
                    StoreStation.GetDayCounter());
            });
            player.Insert(HospitalStoryPoints.MaterialDelivered);
        }
    }
}
