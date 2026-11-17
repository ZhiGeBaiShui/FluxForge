using FluxForge.Story.AcidBuyer;
using FluxForge.Story.Hospital;

namespace FluxForge.Story.AcidBuyerFirstHighPurity
{
    internal sealed class AcidBuyerHighPurityAnswerEdge : StoryEdge
    {
        private readonly string _responseKey;

        internal AcidBuyerHighPurityAnswerEdge(
            string label,
            string response)
            : base(label, "question")
        {
            _responseKey = response;
        }

        internal override void Execute(StorySequence player)
        {
            AcidBuyerProgress.MarkHighPurityDialogueDone();
            HospitalContractState.QueueOffer();
            player.SayNpc(ModHelper.GetLocalized("fluxforge", _responseKey));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerFirstHighPurityTexts.ContinueQuestion));
        }
    }
}
