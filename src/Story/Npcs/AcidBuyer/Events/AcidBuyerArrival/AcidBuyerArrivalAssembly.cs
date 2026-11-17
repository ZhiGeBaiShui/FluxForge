using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

namespace FluxForge.Story.AcidBuyerArrival
{
    /// <summary>首次到访：开场中赠送原料，再进入交易选项；正常结束时记录已见面。</summary>
    internal sealed class AcidBuyerArrivalAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal override void Complete()
        {
            AcidBuyerProgress.MarkMet();
        }

        internal AcidBuyerArrivalAssembly()
        {
            AcidBuyerTradeSession session = new AcidBuyerTradeSession();
            StoryNode root = new StoryNode();
            StoryNode success = new StoryNode();
            StoryNode highPurity = new StoryNode();
            AcidBuyerGreetingEdge greeting = new AcidBuyerGreetingEdge(session);
            AcidBuyerTradeEdge trade = new AcidBuyerTradeEdge(session);
            AcidBuyerTradeEdge tradeAgain = new AcidBuyerTradeEdge(session);
            AcidBuyerExitEdge exit = new AcidBuyerExitEdge();
            AcidBuyerSoldOutEdge soldOut = new AcidBuyerSoldOutEdge(session);
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
