
namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractTerminationEdge : StoryEdge
    {
        internal HospitalContractTerminationEdge()
            : base(null, null)
        {
        }

        internal override void Execute(StorySequence player)
        {
            HospitalContractState.Terminate();
            player.SaySystem(ModHelper.GetLocalized("fluxforge", HospitalContractTerminationTexts.Black));
            player.SaySystem(ModHelper.GetLocalized("fluxforge", HospitalContractTerminationTexts.Gray));
        }
    }
}
