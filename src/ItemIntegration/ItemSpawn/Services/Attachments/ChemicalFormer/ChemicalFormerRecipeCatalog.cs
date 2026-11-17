using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>FluxForge 成型机配方目录；只登记本 Mod 的配方。</summary>
    internal static class ChemicalFormerRecipeCatalog
    {
        internal const string SelectedRecipeTag
            = "FLUXFORGE_CHEM_FORMER_RECIPE_ID";
        private static readonly List<IChemicalFormerRecipe> Recipes = new()
        {
            new HeavyMetalFluxRecipe(
                "fluxforge_heavy_metal_flux_agent",
                "item_flux_agent_name",
                "former.flux_material",
                "flux_agent",
                4000),
            new HeavyMetalFluxRecipe(
                "fluxforge_heavy_metal_advanced_flux_agent",
                "item_advanced_flux_agent_name",
                "former.advanced_flux_material",
                "advanced_flux_agent",
                400000),
        };

        internal static int Count => Recipes.Count;

        internal static void Register(IChemicalFormerRecipe recipe)
        {
            Recipes.Add(recipe);
        }

        internal static PixelWindow CreateSelector(
            System.Action<string> onSelect,
            out System.Action destroyPreviews)
        {
            GameGridInventory productGrid = new GameGridInventory(8, 4);
            productGrid.SetMachineryStyle();
            List<GameItem> previews = new();
            foreach (IChemicalFormerRecipe recipe in Recipes)
            {
                GameItem preview = recipe.CreateOutput();
                preview.EnableTag("CHOICE_PREVIEW_TAG");
                preview.ModifyTag(
                    "FLUXFORGE_CHEM_FORMER_PREVIEW_RECIPE",
                    state => state.Enable().SetString(recipe.Identifier));
                preview.shortDescription = recipe.MaterialDescription;
                productGrid.TryAcceptAll(preview);
                previews.Add(preview);
            }
            productGrid.mayInventoryAddItemFunc = (_, __) => false;

            PixelWindow selector = null;
            System.Action<GameItem> selectionCallback = item =>
            {
                string identifier = item.GetTagReadonly(
                    "FLUXFORGE_CHEM_FORMER_PREVIEW_RECIPE").GetString();
                onSelect(identifier);
                selector.closeCallback();
                selector.Hide();
            };

            selector = MachineHelper.CreateMachineryWindow(
                true,
                MachineHelper.Panel(ModHelper.GetLocalized(ItemFactory.ModId, "former.select_recipe"), productGrid, "recipes"));
            selector.closeCallback = () =>
            {
                if (RecipeSelectHandler.current.callback == selectionCallback)
                {
                    RecipeSelectHandler.current.callback = null;
                }
            };
            selector.openCallback = () =>
                RecipeSelectHandler.current.callback = selectionCallback;
            destroyPreviews = () =>
            {
                foreach (GameItem preview in previews)
                {
                    preview.Destroy();
                }
                previews.Clear();
            };
            return selector;
        }

        internal static IChemicalFormerRecipe Get(string identifier)
        {
            return Recipes.Find(recipe => recipe.Identifier == identifier);
        }
    }
}
