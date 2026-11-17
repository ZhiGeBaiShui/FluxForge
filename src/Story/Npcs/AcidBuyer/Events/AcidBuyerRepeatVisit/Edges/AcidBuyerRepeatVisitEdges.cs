using FluxForge.Story;
using FluxForge.Story.AcidBuyer;
using FluxForge.Story.AcidBuyerFirstHighPurity;

namespace FluxForge.Story.AcidBuyerRepeatVisit
{
    internal sealed class AcidBuyerGreetingEdge : StoryEdge
    {
        private readonly AcidBuyerTradeSession _session;

        internal AcidBuyerGreetingEdge(AcidBuyerTradeSession session)
            : base(null, null)
        {
            _session = session;
        }

        internal override void Execute(StorySequence player)
        {
            _session.BeginVisit();
            player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.Line1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.Line2));
        }
    }

    internal sealed class AcidBuyerTradeEdge : StoryEdge
    {
        private readonly AcidBuyerTradeSession _session;
        private StoryNode _success;
        private StoryNode _highPurity;
        private StoryNode _failure;

        internal bool Succeeded { get; private set; }

        internal AcidBuyerTradeEdge(AcidBuyerTradeSession session)
            : base(
                AcidBuyerRepeatVisitTexts.TradeOption,
                "fluxforge_test_acid")
        {
            _session = session;
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

            return _session.ShouldShowHighPurityDialogue
                ? _highPurity
                : _success;
        }

        internal override void Execute(StorySequence player)
        {
            AcidBuyerTradeResult result = AcidBuyerTrade.TrySellAll(_session, out _);
            Succeeded = result == AcidBuyerTradeResult.Success;

            if (result == AcidBuyerTradeResult.NoAcidOrBase)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.NoAcidOrBase));
            }
            else if (result == AcidBuyerTradeResult.ConcentrationTooLow)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ConcentrationTooLow));
            }
            else
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.Success));
                if (_session.ShouldShowHighPurityDialogue)
                {
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerFirstHighPurityTexts.Inspection));
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerFirstHighPurityTexts.Question));
                }
                else
                {
                    player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ContinueQuestion));
                }
            }

        }
    }

    internal sealed class AcidBuyerExitEdge : StoryEdge
    {
        internal AcidBuyerExitEdge()
            : base(AcidBuyerRepeatVisitTexts.ExitOption, "question")
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.ExitLine));
        }
    }

    internal sealed class AcidBuyerSoldOutEdge : StoryEdge
    {
        private readonly AcidBuyerTradeSession _session;

        internal AcidBuyerSoldOutEdge(AcidBuyerTradeSession session)
            : base(AcidBuyerRepeatVisitTexts.SoldOutOption, "question")
        {
            _session = session;
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(
                _session.HasSufficientHighPurity
                    ? ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutHigh)
                    : ModHelper.GetLocalized("fluxforge", AcidBuyerRepeatVisitTexts.SoldOutLow));
        }
    }
}
