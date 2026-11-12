namespace FluxForge.Story.Hospital
{
    internal sealed class HospitalContractMaterialAssembly : StoryAssembly
    {
        private readonly StoryEdge _entry;

        internal HospitalContractMaterialAssembly()
        {
            StoryNode root = new StoryNode();
            HospitalContractMaterialGreetingEdge greeting
                = new HospitalContractMaterialGreetingEdge();
            HospitalContractMaterialAllEdge all
                = new HospitalContractMaterialAllEdge();
            HospitalContractMaterialPartEdge part
                = new HospitalContractMaterialPartEdge();
            HospitalContractMaterialDeclineEdge decline
                = new HospitalContractMaterialDeclineEdge();

            greeting.Connect(null, root);
            all.Connect(root, null);
            part.Connect(root, null);
            decline.Connect(root, null);

            _entry = greeting;
        }

        internal override StoryEdge Entry
        {
            get { return _entry; }
        }
    }
}
