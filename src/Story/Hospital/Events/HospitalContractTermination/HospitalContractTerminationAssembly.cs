namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractTerminationAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractTerminationAssembly()
        {
            HospitalContractTerminationEdge termination
                = new HospitalContractTerminationEdge();
            termination.Connect(null, null);
            _entry = termination;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
