using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 化学扫描仪面板的流源装置分支。
    ///
    /// 原版 RefreshUI 会读取 WaterFeature / CHEMICAL_PRODUCT_TAG，而装置容器只有
    /// FlowProps tag；这里在进入原逻辑前直接填充同一组 TMP 字段并隐藏原版按钮/效果。
    /// 面板仍由 ChemScannerUIManager.Open 打开，不另建 UI。
    /// </summary>
    [HarmonyPatch(typeof(ChemScannerUIManager), nameof(ChemScannerUIManager.RefreshUI))]
    internal static class FlowScannerRefreshPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(ChemScannerUIManager __instance)
        {
            GameItem item = __instance.currentScannedItem;
            if (item == null || !item.IsTag(FlowTags.DeviceContainerTag))
            {
                return true;
            }

            FlowProperties properties = FlowStock.Read(item);
            double total = properties.SubstanceTotal;

            __instance.productNameTMP.text = item.name;
            __instance.testedTMP.text = ModHelper.GetLocalized(ItemFactory.ModId, "scanner.flow_device");
            __instance.labelTMP.text = FlowScannerText.Temperature(properties);
            __instance.contaminantTMP.text =
                FlowScannerText.BuildComposition(properties, total);
            __instance.totalVolumeTMP.text = total > 0d
                ? ModHelper.GetLocalized(ItemFactory.ModId, "scanner.total", WaterScannerUIManager.FormatAmount((int)total))
                : "";

            __instance.confrontButton.gameObject.SetActive(false);
            __instance.matchButton.gameObject.SetActive(false);
            __instance.euphoriaEffect.gameObject.SetActive(false);
            __instance.toxicityEffect.gameObject.SetActive(false);
            __instance.synaprestEffect.gameObject.SetActive(false);
            __instance.birthControlEffect.gameObject.SetActive(false);
            return false;
        }
    }
}
