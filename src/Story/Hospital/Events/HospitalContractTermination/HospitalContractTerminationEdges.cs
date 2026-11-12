using System.Threading.Tasks;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractTerminationEdge : StoryEdge
    {
        internal HospitalContractTerminationEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            HospitalContractState.Terminate();
            player.SaySystem(HospitalContractTerminationTexts.Black);
            player.SaySystem(HospitalContractTerminationTexts.Gray);
            return Task.CompletedTask;
        }
    }
}
