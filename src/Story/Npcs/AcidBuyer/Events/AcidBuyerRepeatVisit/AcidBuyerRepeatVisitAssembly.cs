using FluxForge.Story.AcidBuyer;

namespace FluxForge.Story.AcidBuyerRepeatVisit
{
    /// <summary>再次到访：两句开场、赠送原料，然后进入交易选项。</summary>
    internal sealed class AcidBuyerRepeatVisitAssembly : NpcStoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override StoryPhase Phase
        {
            get { return StoryPhase.GenericVisit; }
        }

        internal override bool CanPlay()
        {
            return AcidBuyerProgress.HasMet;
        }

        internal AcidBuyerRepeatVisitAssembly()
        {
            StoryNode root = new StoryNode();
            StoryNode success = new StoryNode();
            AcidBuyerGreetingEdge greeting = new AcidBuyerGreetingEdge();
            AcidBuyerTradeEdge trade = new AcidBuyerTradeEdge();
            AcidBuyerExitEdge exit = new AcidBuyerExitEdge();
            AcidBuyerSuccessExitEdge successExit
                = new AcidBuyerSuccessExitEdge();

            greeting.Connect(null, root);
            trade.Connect(root, root);
            trade.Branch(delegate { return trade.Succeeded; }, success, root);
            exit.Connect(root, null);
            successExit.Connect(success, null);

            Entry = greeting;
        }
    }
}
