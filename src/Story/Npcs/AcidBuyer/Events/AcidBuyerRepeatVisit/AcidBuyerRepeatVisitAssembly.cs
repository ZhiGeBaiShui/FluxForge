using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

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
            StoryNode highPurity = new StoryNode();
            AcidBuyerGreetingEdge greeting = new AcidBuyerGreetingEdge();
            AcidBuyerTradeEdge trade = new AcidBuyerTradeEdge();
            AcidBuyerTradeEdge tradeAgain = new AcidBuyerTradeEdge();
            AcidBuyerExitEdge exit = new AcidBuyerExitEdge();
            AcidBuyerSoldOutEdge soldOut = new AcidBuyerSoldOutEdge();
            AcidBuyerHighPurityAnswerEdge selfMade
                = new AcidBuyerHighPurityAnswerEdge(
                    AcidBuyerFirstHighPurityTexts.SelfMadeOption,
                    AcidBuyerFirstHighPurityTexts.SelfMadeResponse);
            AcidBuyerHighPurityAnswerEdge luck
                = new AcidBuyerHighPurityAnswerEdge(
                    AcidBuyerFirstHighPurityTexts.LuckOption,
                    AcidBuyerFirstHighPurityTexts.LuckResponse);

            greeting.Connect(null, root);
            trade.Connect(root, success);
            trade.SetTargets(success, highPurity, root);
            exit.Connect(root, null);

            tradeAgain.Connect(success, success);
            tradeAgain.SetTargets(success, highPurity, success);
            soldOut.Connect(success, null);

            selfMade.Connect(highPurity, success);
            luck.Connect(highPurity, success);

            Entry = greeting;
        }
    }
}
