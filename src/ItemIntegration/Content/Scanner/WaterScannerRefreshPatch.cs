using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 水扫描仪面板的流源装置分支。
    ///
    /// 原版 RefreshUI 会读取 WaterFeature / 水纯度；装置容器没有 WaterFeature，
    /// 这里在进入原逻辑前填充同一组 TMP 字段，保留 AquaScan 自己的面板贴图。
    /// </summary>
    [HarmonyPatch(typeof(WaterScannerUIManager), nameof(WaterScannerUIManager.RefreshUI))]
    internal static class WaterScannerRefreshPatch
    {
        [HarmonyPrefix]
        private static bool Prefix(WaterScannerUIManager __instance)
        {
            GameItem item = __instance.currentWaterContainer;
            if (item == null || !item.IsTag(FlowTags.DeviceContainerTag))
            {
                return true;
            }

            FlowProperties properties = FlowStock.Read(item);
            double total = properties.SubstanceTotal;

            __instance.testedTMP.text = item.name
                + "\n" + ModHelper.GetLocalized(ItemFactory.ModId, "scanner.total", WaterScannerUIManager.FormatAmount((int)total));
            __instance.labelTMP.text = ModHelper.GetLocalized(ItemFactory.ModId, "scanner.flow_device") + "\n"
                + FlowScannerText.Temperature(properties);
            __instance.contaminantTMP.text =
                FlowScannerText.BuildComposition(properties, total);

            __instance.confrontButton.gameObject.SetActive(false);
            __instance.matchButton.gameObject.SetActive(false);
            return false;
        }
    }
}
