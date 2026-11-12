namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractMilestoneAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractMilestoneAssembly(int tier)
        {
            HospitalContractMilestoneEdge milestone
                = new HospitalContractMilestoneEdge(tier);
            milestone.Connect(null, null);
            _entry = milestone;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
