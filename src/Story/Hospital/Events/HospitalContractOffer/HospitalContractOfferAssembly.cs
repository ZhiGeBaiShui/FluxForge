namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractOfferAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractOfferAssembly()
        {
            StoryNode root = new StoryNode();
            HospitalContractOfferGreetingEdge greeting
                = new HospitalContractOfferGreetingEdge();
            HospitalContractOfferAskEdge ask
                = new HospitalContractOfferAskEdge();
            HospitalContractOfferAcceptEdge accept
                = new HospitalContractOfferAcceptEdge();
            HospitalContractOfferDeclineEdge decline
                = new HospitalContractOfferDeclineEdge();
            HospitalContractOfferNoInterestEdge noInterest
                = new HospitalContractOfferNoInterestEdge();

            greeting.Connect(null, root);
            accept.Connect(root, null);
            ask.Connect(root, root);
            decline.Connect(root, null);
            noInterest.Connect(root, null);

            _entry = greeting;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
