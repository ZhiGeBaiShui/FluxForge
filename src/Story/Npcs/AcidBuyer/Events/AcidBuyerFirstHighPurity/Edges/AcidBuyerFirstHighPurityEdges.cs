using System.Threading.Tasks;
using FluxForge.Story.AcidBuyer;

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
            player.SayNpc(_response);
            return Task.CompletedTask;
        }
    }
}
