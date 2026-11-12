using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

namespace FluxForge.Story.AcidBuyerArrival
{
    /// <summary>首次到访：开场、赠送原料，然后进入交易选项。</summary>
    internal sealed class AcidBuyerArrivalAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override void Complete()
        {
            AcidBuyerProgress.MarkMet();
        }

        internal AcidBuyerArrivalAssembly()
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
