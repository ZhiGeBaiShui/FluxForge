using System.Threading.Tasks;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>每周固定送料：没有选项，说完直接把本周原料放下。</summary>
    internal sealed class HospitalContractMaterialEdge : StoryEdge
    {
        internal HospitalContractMaterialEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractMaterialTexts.Greeting);
            player.Do(delegate
            {
                HospitalContractStock.GiveWeekly(
                    HospitalContractStock.WeeklyCount());
                HospitalContractState.MarkMaterial(
                    StoreStation.GetDayCounter());
            });
            return Task.CompletedTask;
        }
    }
}
