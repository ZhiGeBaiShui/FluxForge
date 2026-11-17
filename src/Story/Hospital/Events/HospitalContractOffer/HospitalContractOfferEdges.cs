using FluxForge.Mod;
using FluxForge.Story.AcidBuyer;

namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractOfferGreetingEdge : StoryEdge
    {
        private readonly AcidBuyerTradeSession _session;
        private readonly IStoryState _state;
        private StoryNode _firstRoot;
        private StoryNode _repeatRoot;
        private StoryNode _repeatAfterAsk;
        private StoryNode _next;

        internal HospitalContractOfferGreetingEdge(
            AcidBuyerTradeSession session, IStoryState state)
            : base(null, null)
        {
            _session = session;
            _state = state;
        }

        internal void SetTargets(
            StoryNode firstRoot,
            StoryNode repeatRoot,
            StoryNode repeatAfterAsk)
        {
            _firstRoot = firstRoot;
            _repeatRoot = repeatRoot;
            _repeatAfterAsk = repeatAfterAsk;
        }

        internal override StoryNode ResolveNext()
        {
            return _next;
        }

        internal override void Execute(StorySequence player)
        {
            _session.BeginVisit();
            if (_state.SeenCount(HospitalContractOfferAssembly.EventId) > 0
                || HospitalContractState.DetailsExplained)
            {
                _next = HospitalContractState.DetailsExplained
                    ? _repeatAfterAsk : _repeatRoot;
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.RepeatGreeting1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.RepeatGreeting2));
            }
            else
            {
                _next = _firstRoot;
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.Greeting1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.Greeting2));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.Greeting3));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.Greeting4));
            }
        }
    }

    internal sealed class HospitalContractOfferAskEdge : StoryEdge
    {
        private const string PriceBody = "paper.contract.body";

        internal HospitalContractOfferAskEdge()
            : base(HospitalContractOfferTexts.AskOption, "question")
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskLine1));
            player.Do(delegate
            {
                PlayerStore.Instance.AddDirectSellingItemToTable(
                    CreatePriceNote(),
                    true);
                HospitalContractState.MarkDetailsExplained();
            });
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskLine2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskLine3));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AskLine4));
        }

        private static GameItem CreatePriceNote()
        {
            GameItem note = PreBuiltItemHelper.CreateHandnote(
                ModHelper.GetLocalized("fluxforge", PriceBody),
                ModHelper.GetLocalized("fluxforge", "paper.contract.title"),
                ModHelper.GetLocalized("fluxforge", "npc.vera.name"),
                2);
            note.name = ModHelper.GetLocalized("fluxforge", "paper.contract.title");
            note.shortDescription = ModHelper.GetLocalized("fluxforge", "paper.contract.description");
            note.unitValue = 0L;
            return note;
        }
    }

    internal sealed class HospitalContractOfferAcceptEdge : StoryEdge
    {
        internal HospitalContractOfferAcceptEdge()
            : base(HospitalContractOfferTexts.AcceptOption, "question")
        {
        }

        internal override void Execute(StorySequence player)
        {
            HospitalContractState.Start(StoreStation.GetDayCounter());
            HospitalReputation.EnsureFaction();
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AcceptLine1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AcceptLine2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.AcceptLine3));
        }
    }

    internal sealed class HospitalContractOfferDeclineEdge : StoryEdge
    {
        internal HospitalContractOfferDeclineEdge(string label)
            : base(label, "question")
        {
        }

        internal override void Execute(StorySequence player)
        {
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.DeclineLine));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.ResumeTrade));
        }
    }

    internal sealed class HospitalContractOfferNoInterestEdge : StoryEdge
    {
        internal HospitalContractOfferNoInterestEdge()
            : base(HospitalContractOfferTexts.NoInterestOption, "question")
        {
        }

        internal override void Execute(StorySequence player)
        {
            HospitalContractState.DeclineOffer();
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.NoInterestLine1));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.NoInterestLine2));
            player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractOfferTexts.ResumeTrade));
        }
    }
}
