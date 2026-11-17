using TMPro;

namespace FluxForge.Mod
{
    /// <summary>
    /// FluxForge 成分成型机 attachment。机械面板复用原版组件，配方完全由 Mod 目录提供。
    /// </summary>
    public sealed class ChemicalFormerAttachment : ItemAttachment
    {
        public override string Name => "chemical_former";

        public override void Attach(GameItem machine, FlowItemDefinition def)
        {
            GameSlotInventory batterySlot
                = MachineryAttachmentFactory.CreateBatterySlot(machine);
            GameSlotInventory containerSlot = new GameSlotInventory();
            GameGridInventory outputGrid = new GameGridInventory(6, 4);
            containerSlot.SetMachineryStyle();
            outputGrid.SetMachineryStyle();
            MachineryButton configureButton = new MachineryButton(
                LocHelper.GetLocalizedMechanic("mech_chem_finisher_btn_configure"));
            MachineryText targetLabel = new MachineryText(
                LocHelper.GetLocalizedMechanic("mech_chem_finisher_no_target"),
                6, HorizontalAlignmentOptions.Center, uppercase: false);
            MachineryText statusLabel = new MachineryText(
                LocHelper.GetLocalizedMechanic("mech_chem_finisher_status_not_enough_reagent"),
                6, HorizontalAlignmentOptions.Center, uppercase: false);
            MachineryButton outputButton = new MachineryButton(
                LocHelper.GetLocalizedMechanic("mech_chem_finisher_btn_output"));
            MachineryStack controlStack = MachineryStack.Vertical(
                configureButton, targetLabel, statusLabel, outputButton);
            configureButton.SetSize(75);
            if (ChemicalFormerRecipeCatalog.Count == 0)
            {
                configureButton.Disable();
            }

            SetupLiquidSlot(containerSlot);
            PixelWindow recipeSelector = null;
            System.Action destroyPreviews;
            recipeSelector = ChemicalFormerRecipeCatalog.CreateSelector(
                identifier =>
                {
                    machine.ModifyTag(
                        ChemicalFormerRecipeCatalog.SelectedRecipeTag,
                        state => state.Enable().SetString(identifier));
                },
                out destroyPreviews);
            recipeSelector.Hide();

            PixelWindow contentWindow = MachineryAttachmentFactory.CreateWindow(
                MachineHelper.PowerPanel(batterySlot),
                MachineHelper.Panel(
                    LocHelper.GetLocalizedMechanic("mech_chem_finisher_label_container"),
                    containerSlot, "container"),
                MachineHelper.Panel(
                    LocHelper.GetLocalizedMechanic("mech_chem_finisher_label_output"),
                    outputGrid, "output"),
                MachineHelper.Panel(
                    LocHelper.GetLocalizedMechanic("mech_chem_finisher_label_control"),
                    controlStack, "control"));
            contentWindow.handler.gameObject
                .AddComponent<ChemicalFormerSelectorLifecycle>()
                .Bind(recipeSelector, destroyPreviews);
            machine.SetContentWindow(contentWindow);
            machine.SetGameItemType("MACHINE");
            machine.EnableTag("CONTAINER_TAG");
            AudioHelper.InitMetalMachine(machine);

            configureButton.SetCallback(() =>
            {
                recipeSelector.Show();
                recipeSelector.Center();
            });
            outputButton.SetCallback(() =>
                Produce(machine, containerSlot, outputGrid, batterySlot,
                    def.chemFormerEnergyCost));
            machine.onOneSecondItemFunc = (_, __, ___) =>
                RefreshStatus(machine, containerSlot, batterySlot,
                    controlStack, targetLabel, statusLabel,
                    def.chemFormerEnergyCost);

            outputGrid.mayInventoryAddItemFunc = (addedItem, inv) =>
                ContainerHelper.CanContainerStoredItem(addedItem, inv);
        }

        private static void SetupLiquidSlot(GameSlotInventory slot)
        {
            slot.mayInventoryAddItemFunc = (item, _) =>
                GeneralHelper.IsItemOwned(item)
                && item.IsTag("LIQUID_CONTAINER_TAG");
            slot.SetBackgroundFadeSprite("Items/water_container4", "xl");
            slot.slotPriorityFunc = (_, __, ___) => 5;
        }

        private static void RefreshStatus(
            GameItem machine,
            GameSlotInventory containerSlot,
            GameSlotInventory batterySlot,
            MachineryStack controlStack,
            MachineryText targetLabel,
            MachineryText statusLabel,
            int energyCost)
        {
            if (ChemicalFormerRecipeCatalog.Count == 0)
            {
                targetLabel.SetText(LocHelper.GetLocalizedMechanic(
                    "mech_chem_finisher_no_target"));
                statusLabel.SetText(ModHelper.GetLocalized(ItemFactory.ModId, "former.no_recipes"));
                return;
            }

            if (!machine.IsTag(ChemicalFormerRecipeCatalog.SelectedRecipeTag))
            {
                targetLabel.SetText(LocHelper.GetLocalizedMechanic(
                    "mech_chem_finisher_no_target"));
                statusLabel.SetText(LocHelper.GetLocalizedMechanic(
                    "mech_turbo_booster_status",
                    LocHelper.GetLocalizedMechanic(
                        "mech_chem_finisher_status_no_target")));
                return;
            }

            IChemicalFormerRecipe recipe = ResolveRecipe(machine);
            targetLabel.SetText(recipe.DisplayName);
            controlStack.Validate();

            if (!CanOutput(machine, containerSlot))
            {
                statusLabel.SetText(ModHelper.GetLocalized(ItemFactory.ModId, "former.wrong_material"));
                return;
            }

            string status = MachineryAttachmentFactory.CanDrawPower(
                batterySlot, energyCost)
                ? "mech_turbo_booster_status_ready"
                : "mech_machinery_power_insufficient";
            statusLabel.SetText(LocHelper.GetLocalizedMechanic(
                "mech_turbo_booster_status",
                LocHelper.GetLocalizedMechanic(status)));
        }

        private static bool CanOutput(
            GameItem machine,
            GameSlotInventory containerSlot)
        {
            if (containerSlot.childItem == null
                || !machine.IsTag(ChemicalFormerRecipeCatalog.SelectedRecipeTag)
                || WaterHelper.IsEmpty(containerSlot.childItem))
            {
                return false;
            }

            return ResolveRecipe(machine).CanOutput(containerSlot.childItem);
        }

        private static void Produce(
            GameItem machine,
            GameSlotInventory containerSlot,
            GameGridInventory outputGrid,
            GameSlotInventory batterySlot,
            int energyCost)
        {
            if (!CanOutput(machine, containerSlot))
            {
                return;
            }

            IChemicalFormerRecipe recipe = ResolveRecipe(machine);
            GameItem product = recipe.CreateOutput();
            if (!GraphUtils.CanAccept(outputGrid, product))
            {
                product.Destroy();
                return;
            }

            if (!MachineryAttachmentFactory.DrawPower(batterySlot, energyCost))
            {
                product.Destroy();
                return;
            }

            recipe.ConsumeInput(containerSlot.childItem, product);
            GraphUtils.TryAcceptAll(outputGrid, product);
            ChemicalProductHelper.GradeProduct(product);
            AchievementTracker.Instance.CheckLabPure(product);
        }

        private static IChemicalFormerRecipe ResolveRecipe(GameItem machine)
        {
            string selectedId = machine.GetTagReadonly(
                ChemicalFormerRecipeCatalog.SelectedRecipeTag)
                .GetString();
            return ChemicalFormerRecipeCatalog.Get(selectedId);
        }
    }
}
