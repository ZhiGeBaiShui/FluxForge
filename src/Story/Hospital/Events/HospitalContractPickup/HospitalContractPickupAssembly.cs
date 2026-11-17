namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupAssembly : StoryAssembly
    {
        internal override StoryEdge Entry { get; }
        internal HospitalContractPickupAssembly()
        {
            HospitalPickupSession session = new HospitalPickupSession();
            HospitalPickupMenu menu = new HospitalPickupMenu();
            StoryNode shortConfirmation = new StoryNode();
            StoryNode zeroConfirmation = new StoryNode();
            HospitalContractPickupGreetingEdge greeting = new HospitalContractPickupGreetingEdge(menu, session);
            greeting.Connect(null, null);
            AddOrdinaryOptions(menu.Ordinary, session, menu, shortConfirmation, zeroConfirmation);
            AddOrdinaryOptions(menu.Task, session, menu, shortConfirmation, zeroConfirmation);
            new HospitalSynaprestSubmitEdge(session, menu).Connect(menu.Task, null);
            new HospitalContractPickupConfirmEdge(session).Connect(shortConfirmation, null);
            new HospitalContractPickupRetryEdge(HospitalContractPickupTexts.RetryOption, menu).Connect(shortConfirmation, null);
            new HospitalContractPickupConfirmEdge(session).Connect(zeroConfirmation, null);
            new HospitalContractPickupRetryEdge(HospitalContractPickupTexts.ZeroRetryOption, menu).Connect(zeroConfirmation, null);
            Entry = greeting;
        }

        private static void AddOrdinaryOptions(StoryNode node, HospitalPickupSession session,
            HospitalPickupMenu menu, StoryNode shortConfirmation, StoryNode zeroConfirmation)
        {
            new HospitalContractPickupSubmitEdge(session, menu).Connect(node, null);
            new HospitalContractPickupFinishEdge(session, shortConfirmation, zeroConfirmation).Connect(node, null);
        }
    }
}
