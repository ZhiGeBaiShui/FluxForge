using FluxForge.Story.AcidBuyer;

namespace FluxForge.Story.AcidBuyerRepeatVisit
{
    /// <summary>再次到访：两句开场，然后进入交易选项。</summary>
    internal sealed class AcidBuyerRepeatVisitAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

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
