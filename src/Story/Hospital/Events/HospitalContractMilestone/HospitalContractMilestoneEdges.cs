
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

        internal override void Execute(StorySequence player)
        {
            if (_tier >= 40)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At40_1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At40_2));
            }
            else
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_2));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_3));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractMilestoneTexts.At20_4));
            }


        }
    }
}
