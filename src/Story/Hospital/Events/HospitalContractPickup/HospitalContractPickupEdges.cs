namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupGreetingEdge : StoryEdge
    {
        private readonly HospitalPickupMenu _menu;
        private readonly HospitalPickupSession _session;
        private readonly StoryNode _requestOptions;
        private bool _offeringRequest;
        internal HospitalContractPickupGreetingEdge(HospitalPickupMenu menu, HospitalPickupSession session)
            : base(null, null)
        {
            _menu = menu;
            _session = session;
            _requestOptions = HospitalHighestDemandOfferAssembly.CreateOptions(menu.Ordinary);
        }
        internal override StoryNode ResolveNext()
        {
            return _offeringRequest ? _requestOptions : _menu.Current;
        }
        internal override void Execute(StorySequence player)
        {
            _session.BeginVisit();
            _offeringRequest = HospitalHighestDemand.CanOffer;
            if (_offeringRequest)
            {
                HospitalHighestDemandOfferEdge.Append(player);
            }
            else
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Greeting));
            }
            if (HospitalSynaprestState.Active)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Ask1));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalSynaprestTexts.Ask2));
            }
        }
    }

    internal sealed class HospitalContractPickupSubmitEdge : StoryEdge
    {
        private readonly HospitalPickupSession _session;
        private readonly HospitalPickupMenu _menu;
        internal HospitalContractPickupSubmitEdge(HospitalPickupSession session, HospitalPickupMenu menu)
            : base(HospitalContractPickupTexts.ConfirmOption, "question") { _session = session; _menu = menu; }
        internal override StoryNode ResolveNext() { return _menu.Current; }
        internal override void Execute(StorySequence player)
        {
            HospitalContractInspection inspection = HospitalContractTrade.Inspect(HospitalContractDefinition.Chemicals);
            if (inspection.HasUnderConcentration)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.UnderConcentration));
            }
            else if (inspection.Qualified.Count == 0)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroInspection));
            }
            else
            {
                _session.Submit(inspection);
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.Continue));
            }
        }
    }

    internal sealed class HospitalContractPickupFinishEdge : StoryEdge
    {
        private readonly HospitalPickupSession _session;
        private readonly StoryNode _shortConfirmation;
        private readonly StoryNode _zeroConfirmation;
        private StoryNode _next;
        internal HospitalContractPickupFinishEdge(HospitalPickupSession session, StoryNode shortConfirmation, StoryNode zeroConfirmation)
            : base(HospitalContractPickupTexts.FinishOption, "question")
        { _session = session; _shortConfirmation = shortConfirmation; _zeroConfirmation = zeroConfirmation; }
        internal override StoryNode ResolveNext() { return _next; }
        internal override void Execute(StorySequence player)
        {
            _next = null;
            if (_session.IsTrustedShortDelivery)
            {
                _session.Finish(player);
            }
            else if (_session.ItemCount == 0)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroInspection));
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ZeroQuestion));
                _next = _zeroConfirmation;
            }
            else if (_session.Score < HospitalContractScore.Target)
            {
                player.SayNpc(ModHelper.GetLocalized("fluxforge", HospitalContractPickupTexts.ShortQuestion));
                _next = _shortConfirmation;
            }
            else
            {
                _session.Finish(player);
            }
        }
    }

    internal sealed class HospitalContractPickupConfirmEdge : StoryEdge
    {
        private readonly HospitalPickupSession _session;
        internal HospitalContractPickupConfirmEdge(HospitalPickupSession session)
            : base(HospitalContractPickupTexts.AcceptShortOption, "question") { _session = session; }
        internal override void Execute(StorySequence player) { _session.Finish(player); }
    }

    internal sealed class HospitalContractPickupRetryEdge : StoryEdge
    {
        private readonly HospitalPickupMenu _menu;
        internal HospitalContractPickupRetryEdge(string label, HospitalPickupMenu menu) : base(label, "question") { _menu = menu; }
        internal override StoryNode ResolveNext() { return _menu.Current; }
        internal override void Execute(StorySequence player) { }
    }
}
