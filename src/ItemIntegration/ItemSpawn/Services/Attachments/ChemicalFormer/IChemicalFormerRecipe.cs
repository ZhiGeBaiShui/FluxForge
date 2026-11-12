namespace FluxForge.Mod
{
    /// <summary>
    /// FluxForge 成型机的专属配方接口；配方由 Mod 自己登记，不读取原版配方表。
    /// </summary>
    internal interface IChemicalFormerRecipe
    {
        string Identifier { get; }

        string DisplayName { get; }

        string MaterialDescription { get; }

        string ProductIdentifier { get; }

        bool CanOutput(GameItem input);

        GameItem CreateOutput();

        void ConsumeInput(GameItem input, GameItem output);
    }
}
