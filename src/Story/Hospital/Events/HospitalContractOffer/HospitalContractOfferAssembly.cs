namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractOfferAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractOfferAssembly()
        {
            StoryNode root = new StoryNode();
            StoryNode afterAsk = new StoryNode();
            HospitalContractOfferGreetingEdge greeting
                = new HospitalContractOfferGreetingEdge();
            HospitalContractOfferAskEdge ask
                = new HospitalContractOfferAskEdge();
            HospitalContractOfferDeclineEdge decline
                = new HospitalContractOfferDeclineEdge();
            HospitalContractOfferNoInterestEdge noInterest
                = new HospitalContractOfferNoInterestEdge();
            HospitalContractOfferAcceptEdge acceptAfterAsk
                = new HospitalContractOfferAcceptEdge();
            HospitalContractOfferDeclineEdge declineAfterAsk
                = new HospitalContractOfferDeclineEdge();
            HospitalContractOfferNoInterestEdge noInterestAfterAsk
                = new HospitalContractOfferNoInterestEdge();

            greeting.Connect(null, root);
            ask.Connect(root, afterAsk);
            decline.Connect(root, null);
            noInterest.Connect(root, null);

            acceptAfterAsk.Connect(afterAsk, null);
            declineAfterAsk.Connect(afterAsk, null);
            noInterestAfterAsk.Connect(afterAsk, null);

            _entry = greeting;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
