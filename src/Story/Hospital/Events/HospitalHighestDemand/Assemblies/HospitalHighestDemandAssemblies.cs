namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalHighestDemandOfferAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry = new HospitalHighestDemandOfferEdge();

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }

        internal HospitalHighestDemandOfferAssembly()
        {
            _entry.Connect(null, CreateOptions(null));
        }

        internal static StoryNode CreateOptions(StoryNode returnTo)
        {
            StoryNode options = new StoryNode();
            new HospitalHighestDemandDecisionEdge(true).Connect(options, returnTo);
            new HospitalHighestDemandDecisionEdge(false).Connect(options, returnTo);
            return options;
        }
    }

    internal sealed class HospitalHighestDemandProgressAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry = new HospitalHighestDemandProgressEdge();

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }

    internal sealed class HospitalHighestDemandCompleteAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry = new HospitalHighestDemandCompleteEdge();

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }

        internal override void Complete()
        {
            HospitalHighestDemandState.Complete();
        }
    }
}
