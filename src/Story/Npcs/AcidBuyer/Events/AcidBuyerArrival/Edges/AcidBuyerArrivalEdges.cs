using System.Threading.Tasks;
using FluxForge.Story.AcidBuyer;

namespace FluxForge.Story.AcidBuyerArrival
{
    internal sealed class AcidBuyerGreetingEdge : StoryEdge
    {
        internal AcidBuyerGreetingEdge()
            : base(null, null)
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerArrivalTexts.Line1);
            player.SayNpc(AcidBuyerArrivalTexts.Line2);
            player.SayNpc(AcidBuyerArrivalTexts.Line3);
            player.Do(AcidBuyerStock.GiveRaw);
            player.SayNpc(AcidBuyerArrivalTexts.Line4);
            player.SayNpc(AcidBuyerArrivalTexts.PriceIntro);
            player.SayNpc(AcidBuyerArrivalTexts.PriceStep);
            player.SayNpc(AcidBuyerArrivalTexts.PricePure);
            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerTradeEdge : StoryEdge
    {
        internal bool Succeeded { get; private set; }

        internal AcidBuyerTradeEdge()
            : base(AcidBuyerArrivalTexts.TradeOption, "fluxforge_test_acid")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            AcidBuyerTradeResult result = AcidBuyerTrade.TrySellAll(out int reward);
            Succeeded = result == AcidBuyerTradeResult.Success;

            if (result == AcidBuyerTradeResult.NoAcidOrBase)
            {
                player.SayNpc(AcidBuyerArrivalTexts.NoAcidOrBase);
            }
            else if (result == AcidBuyerTradeResult.ConcentrationTooLow)
            {
                player.SayNpc(AcidBuyerArrivalTexts.ConcentrationTooLow);
            }
            else
            {
                player.SayNpc(string.Format(
                    AcidBuyerArrivalTexts.Success,
                    reward));
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerSuccessExitEdge : StoryEdge
    {
        internal AcidBuyerSuccessExitEdge()
            : base(AcidBuyerArrivalTexts.SuccessExitOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerArrivalTexts.SuccessLeave);
            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerExitEdge : StoryEdge
    {
        internal AcidBuyerExitEdge()
            : base(AcidBuyerArrivalTexts.ExitOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerArrivalTexts.ExitLine);
            return Task.CompletedTask;
        }
    }
}
