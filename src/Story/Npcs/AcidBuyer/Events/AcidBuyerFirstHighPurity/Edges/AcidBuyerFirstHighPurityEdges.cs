using System.Threading.Tasks;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.Hospital;

namespace FluxForge.Story.AcidBuyerFirstHighPurity
{
    internal sealed class AcidBuyerHighPurityAnswerEdge : StoryEdge
    {
        private readonly string _response;

        internal AcidBuyerHighPurityAnswerEdge(
            string label,
            string response)
            : base(label, "question")
        {
            _response = response;
        }

        internal override Task Execute(StorySequence player)
        {
            AcidBuyerProgress.MarkHighPurityDialogueDone();
            HospitalContractState.QueueOffer();
            player.SayNpc(_response);
            return Task.CompletedTask;
        }
    }
}
