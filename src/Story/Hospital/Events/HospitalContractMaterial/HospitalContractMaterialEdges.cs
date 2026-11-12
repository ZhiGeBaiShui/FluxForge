using System.Threading.Tasks;
using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractMaterialGreetingEdge : StoryEdge
    {
        internal HospitalContractMaterialGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(HospitalContractMaterialTexts.Greeting1);
            player.SayNpc(HospitalContractMaterialTexts.Greeting2);
            player.SayNpc(HospitalContractMaterialTexts.Greeting3);
            player.SayNpc(HospitalContractMaterialTexts.Greeting4);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractMaterialAllEdge : StoryEdge
    {
        internal HospitalContractMaterialAllEdge()
            : base(HospitalContractMaterialTexts.AllOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractStock.GiveWeekly(
                HospitalContractStock.WeeklyCount());
            HospitalContractState.MarkMaterial(
                StoreStation.GetDayCounter());
            player.SayNpc(HospitalContractMaterialTexts.AllLine1);
            player.SayNpc(HospitalContractMaterialTexts.AllLine2);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractMaterialPartEdge : StoryEdge
    {
        internal HospitalContractMaterialPartEdge()
            : base(HospitalContractMaterialTexts.PartOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            int count = HospitalContractStock.WeeklyCount() / 2;
            HospitalContractStock.GiveWeekly(count);
            HospitalContractState.MarkMaterial(
                StoreStation.GetDayCounter());
            player.SayNpc(HospitalContractMaterialTexts.PartLine1);
            player.SayNpc(HospitalContractMaterialTexts.PartLine2);
            return Task.CompletedTask;
        }
    }

    internal sealed class HospitalContractMaterialDeclineEdge : StoryEdge
    {
        internal HospitalContractMaterialDeclineEdge()
            : base(HospitalContractMaterialTexts.DeclineOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractState.MarkMaterial(
                StoreStation.GetDayCounter());
            player.SayNpc(HospitalContractMaterialTexts.DeclineLine1);
            player.SayNpc(HospitalContractMaterialTexts.DeclineLine2);
            return Task.CompletedTask;
        }
    }
}
