namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractMaterialAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractMaterialAssembly()
        {
            HospitalContractMaterialEdge material
                = new HospitalContractMaterialEdge();
            material.Connect(null, null);
            _entry = material;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
