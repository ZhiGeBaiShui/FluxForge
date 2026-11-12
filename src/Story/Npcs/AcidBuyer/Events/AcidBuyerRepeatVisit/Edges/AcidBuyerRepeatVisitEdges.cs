using System.Threading.Tasks;
using FluxForge.Story;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

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
        private StoryNode _success;
        private StoryNode _highPurity;
        private StoryNode _failure;

        internal bool Succeeded { get; private set; }

        internal AcidBuyerTradeEdge()
            : base(AcidBuyerRepeatVisitTexts.TradeOption, "fluxforge_test_acid")
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
                player.SayNpc(AcidBuyerRepeatVisitTexts.NoAcidOrBase);
            }
            else if (result == AcidBuyerTradeResult.ConcentrationTooLow)
            {
                player.SayNpc(AcidBuyerRepeatVisitTexts.ConcentrationTooLow);
            }
            else
            {
                player.SayNpc(AcidBuyerRepeatVisitTexts.Success);
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
            : base(AcidBuyerRepeatVisitTexts.ExitOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(AcidBuyerRepeatVisitTexts.ExitLine);
            return Task.CompletedTask;
        }
    }

    internal sealed class AcidBuyerSoldOutEdge : StoryEdge
    {
        internal AcidBuyerSoldOutEdge()
            : base(AcidBuyerRepeatVisitTexts.SoldOutOption, "question")
        {
        }

        internal override Task Execute(StorySequence player)
        {
            player.SayNpc(
                AcidBuyerProgress.LastHighPurityDeliveryVolume
                    >= AcidBuyerProgress.HighPurityDialogueThreshold
                    ? AcidBuyerRepeatVisitTexts.SoldOutHigh
                    : AcidBuyerRepeatVisitTexts.SoldOutLow);
            return Task.CompletedTask;
        }
    }
}
