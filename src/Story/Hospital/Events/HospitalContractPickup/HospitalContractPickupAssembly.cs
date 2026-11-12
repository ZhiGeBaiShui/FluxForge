namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractPickupAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractPickupAssembly()
        {
            HospitalContractPickupEdge pickup
                = new HospitalContractPickupEdge();
            pickup.Connect(null, null);
            _entry = pickup;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
