using System.Threading.Tasks;
using FluxForge.Story;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

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
        private StoryNode _success;
        private StoryNode _highPurity;
        private StoryNode _failure;

        internal bool Succeeded { get; private set; }

        internal AcidBuyerTradeEdge(string label = null)
            : base(
                label ?? AcidBuyerArrivalTexts.TradeOption,
                "fluxforge_test_acid")
        {
        }

        internal void SetTargets(
            StoryNode success,
            StoryNode highPurity,
            StoryNode failure)
        {
            _success = success;
            _highPurity = highPurity;
            _failure = failure;
        }

        internal override StoryNode ResolveNext()
        {
            if (!Succeeded)
            {
                return _failure;
            }

            return AcidBuyerProgress.ShouldShowHighPurityDialogue
                ? _highPurity
                : _success;
        }

        internal override Task Execute(StorySequence player)
        {
            AcidBuyerTradeResult result = AcidBuyerTrade.TrySellAll(out _);
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
                player.SayNpc(AcidBuyerArrivalTexts.Success);
                if (AcidBuyerProgress.ShouldShowHighPurityDialogue)
                {
                    player.SayNpc(AcidBuyerFirstHighPurityTexts.Question);
                }
            }

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

    internal sealed class AcidBuyerSoldOutEdge : StoryEdge
    {
        internal AcidBuyerSoldOutEdge()
            : base(AcidBuyerArrivalTexts.SoldOutOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(
                AcidBuyerProgress.LastHighPurityDeliveryVolume
                    >= AcidBuyerProgress.HighPurityDialogueThreshold
                    ? AcidBuyerArrivalTexts.SoldOutHigh
                    : AcidBuyerArrivalTexts.SoldOutLow);
            return Task.CompletedTask;
        }
    }
}
