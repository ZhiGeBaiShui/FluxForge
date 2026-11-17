namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalSynaprestOfferAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }

        internal HospitalSynaprestOfferAssembly()
        {
            StoryNode options = new StoryNode();
            HospitalSynaprestGreetingEdge greeting = new HospitalSynaprestGreetingEdge();
            greeting.Connect(null, options);
            new HospitalSynaprestAcceptEdge().Connect(options, null);
            new HospitalSynaprestDeclineEdge().Connect(options, null);
            Entry = greeting;
        }
    }
}
