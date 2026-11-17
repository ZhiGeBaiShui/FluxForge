namespace FluxForge.Mod
{
    /// <summary>将纯重金属按配方用量转化为原版助溶剂。</summary>
    internal sealed class HeavyMetalFluxRecipe : IChemicalFormerRecipe
    {
        private readonly string _identifier;
        private readonly string _displayNameKey;
        private readonly string _materialDescriptionKey;
        private readonly string _productIdentifier;
        private readonly int _requiredHeavyMetal;

        internal HeavyMetalFluxRecipe(
            string identifier,
            string displayName,
            string materialDescription,
            string productIdentifier,
            int requiredHeavyMetal)
        {
            _identifier = identifier;
            _displayNameKey = displayName;
            _materialDescriptionKey = materialDescription;
            _productIdentifier = productIdentifier;
            _requiredHeavyMetal = requiredHeavyMetal;
        }

        public string Identifier => _identifier;

        public string DisplayName => LocHelper.GetLocalizedItem(_displayNameKey);

        public string MaterialDescription => ModHelper.GetLocalized(ItemFactory.ModId, _materialDescriptionKey);

        public string ProductIdentifier => _productIdentifier;

        public bool CanOutput(GameItem input)
        {
            FlowProperties contents = FlowStock.Read(input);
            return contents.Get(FlowProps.HeavyMetal) >= _requiredHeavyMetal
                && contents.Get(FlowProps.HeavyMetal) == contents.SubstanceTotal;
        }

        public GameItem CreateOutput()
        {
            return DirectoryMaster.Item(ProductIdentifier);
        }

        public void ConsumeInput(GameItem input, GameItem output)
        {
            FlowProperties contents = FlowStock.Read(input);
            contents.Set(
                FlowProps.HeavyMetal,
                contents.Get(FlowProps.HeavyMetal) - _requiredHeavyMetal);
            FlowStock.Write(input, contents);
        }
    }
}
