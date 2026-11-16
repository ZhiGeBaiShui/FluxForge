using System.Globalization;
using System.Text;
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
            __instance.testedTMP.text = "流源装置";
            __instance.labelTMP.text = "温度："
                + properties.Get(FlowProps.Temperature).ToString("0.#", CultureInfo.InvariantCulture)
                + "℃";
            __instance.contaminantTMP.text = BuildComposition(properties, total);
            __instance.totalVolumeTMP.text = total > 0d
                ? "总存量：" + WaterScannerUIManager.FormatAmount((int)total)
                : "";

            __instance.confrontButton.gameObject.SetActive(false);
            __instance.matchButton.gameObject.SetActive(false);
            __instance.euphoriaEffect.gameObject.SetActive(false);
            __instance.toxicityEffect.gameObject.SetActive(false);
            __instance.synaprestEffect.gameObject.SetActive(false);
            __instance.birthControlEffect.gameObject.SetActive(false);
            return false;
        }

        /// <summary>
        /// 固定遍历注册表（38 项），不是库存遍历；只写非零液体。
        /// LiquidDict 里没有 foreign，因此外来液体单独命名。
        /// </summary>
        private static string BuildComposition(FlowProperties properties, double total)
        {
            StringBuilder builder = new StringBuilder(256);

            for (int i = 0; i < FlowPropertyRegistry.All.Count; i++)
            {
                FlowPropertyDef def = FlowPropertyRegistry.All[i];
                if (def.Kind != FlowPropertyKind.Liquid
                    && def.Kind != FlowPropertyKind.Foreign)
                {
                    continue;
                }

                double amount = properties.Get(def.Key);
                if (amount <= 0d)
                {
                    continue;
                }

                string name = def.Kind == FlowPropertyKind.Foreign
                    ? "外来液体"
                    : Liquid.LiquidDict[def.Id].displayName;

                builder.Append(name)
                    .Append("：")
                    .Append(WaterScannerUIManager.FormatAmount((int)amount));

                if (total > 0d)
                {
                    builder.Append(" (")
                        .Append((amount / total * 100d).ToString("0.#", CultureInfo.InvariantCulture))
                        .Append("%)");
                }

                builder.Append('\n');
            }

            return builder.ToString();
        }
    }
}
