using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerRepeatVisit;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractOfferAssembly : StoryAssembly
    {
        internal const string EventId = "acid_buyer.contract_offer";

        private readonly StoryEdge _entry;

        internal HospitalContractOfferAssembly(IStoryState state)
        {
            AcidBuyerTradeSession session = new AcidBuyerTradeSession();
            StoryNode selling = AcidBuyerRepeatVisitAssembly.CreateTradeOptions(session);
            var first = CreateOptions(selling, HospitalContractOfferTexts.DeclineOption);
            var repeat = CreateOptions(selling, HospitalContractOfferTexts.RepeatDeclineOption);
            HospitalContractOfferGreetingEdge greeting
                = new HospitalContractOfferGreetingEdge(session, state);
            greeting.SetTargets(first.Root, repeat.Root, repeat.AfterAsk);
            greeting.Connect(null, first.Root);
            _entry = greeting;
        }

        private static (StoryNode Root, StoryNode AfterAsk) CreateOptions(
            StoryNode selling, string declineLabel)
        {
            StoryNode root = new StoryNode();
            StoryNode afterAsk = new StoryNode();
            new HospitalContractOfferAskEdge().Connect(root, afterAsk);
            new HospitalContractOfferDeclineEdge(declineLabel).Connect(root, selling);
            new HospitalContractOfferNoInterestEdge().Connect(root, selling);

            new HospitalContractOfferAcceptEdge().Connect(afterAsk, null);
            new HospitalContractOfferDeclineEdge(declineLabel).Connect(afterAsk, selling);
            new HospitalContractOfferNoInterestEdge().Connect(afterAsk, selling);
            return (root, afterAsk);
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
