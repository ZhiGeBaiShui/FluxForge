using System.Threading.Tasks;
using FluxForge.Story.AcidBuyer;

namespace FluxForge.Story.AcidBuyerRepeatVisit
{
    internal sealed class AcidBuyerGreetingEdge : StoryEdge
    {
        internal AcidBuyerGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerRepeatVisitTexts.Line1);
            player.SayNpc(AcidBuyerRepeatVisitTexts.Line2);
            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerTradeEdge : StoryEdge
    {
        internal bool Succeeded { get; private set; }

        internal AcidBuyerTradeEdge()
            : base(AcidBuyerRepeatVisitTexts.TradeOption, "fluxforge_test_acid")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            AcidBuyerTradeResult result = AcidBuyerTrade.TrySellAll(out int reward);
            Succeeded = result == AcidBuyerTradeResult.Success;

            if (result == AcidBuyerTradeResult.NoAcidOrBase)
            {
                player.SayNpc(AcidBuyerRepeatVisitTexts.NoAcidOrBase);
            }
            else if (result == AcidBuyerTradeResult.ConcentrationTooLow)
            {
                player.SayNpc(AcidBuyerRepeatVisitTexts.ConcentrationTooLow);
            }
            else
            {
                player.SayNpc(string.Format(
                    AcidBuyerRepeatVisitTexts.Success,
                    reward));
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerSuccessExitEdge : StoryEdge
    {
        internal AcidBuyerSuccessExitEdge()
            : base(AcidBuyerRepeatVisitTexts.SuccessExitOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerRepeatVisitTexts.SuccessLeave);
            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerExitEdge : StoryEdge
    {
        internal AcidBuyerExitEdge()
            : base(AcidBuyerRepeatVisitTexts.ExitOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerRepeatVisitTexts.ExitLine);
            return Task.CompletedTask;
        }
    }
}
