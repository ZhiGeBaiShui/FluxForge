namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractPickupAssembly()
        {
            StoryNode confirm = new StoryNode();
            HospitalContractPickupGreetingEdge greeting
                = new HospitalContractPickupGreetingEdge();
            HospitalContractPickupConfirmEdge pickup
                = new HospitalContractPickupConfirmEdge();

            greeting.Connect(null, confirm);
            pickup.Connect(confirm, null);

            _entry = greeting;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
