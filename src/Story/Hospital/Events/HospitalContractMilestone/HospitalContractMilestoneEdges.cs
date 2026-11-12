using System.Threading.Tasks;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractMilestoneEdge : StoryEdge
    {
        private readonly int _tier;

        internal HospitalContractMilestoneEdge(int tier)
            : base(null, null)
        {
            _tier = tier;
        }

        internal override Task Execute(StorySequence player)
        {
            if (_tier >= 50)
            {
                player.SayNpc(HospitalContractMilestoneTexts.At50_1);
                player.SayNpc(HospitalContractMilestoneTexts.At50_2);
            }
            else
            {
                player.SayNpc(HospitalContractMilestoneTexts.At25_1);
                player.SayNpc(HospitalContractMilestoneTexts.At25_2);
                player.SayNpc(HospitalContractMilestoneTexts.At25_3);
            }

            return Task.CompletedTask;
        }
    }
}
