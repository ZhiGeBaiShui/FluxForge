using System.Globalization;
using System.Text;

namespace FluxForge.Mod
{
    /// <summary>
    /// 两种原版扫描仪共用的流源装置文本。
    ///
    /// 只遍历固定的属性注册表，不遍历库存。LiquidDict 里没有 foreign，
    /// 所以外来液体单独命名。
    /// </summary>
    internal static class FlowScannerText
    {
        internal static string BuildComposition(FlowProperties properties, double total)
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

        internal static string Temperature(FlowProperties properties)
        {
            return "温度："
                + properties.Get(FlowProps.Temperature)
                    .ToString("0.#", CultureInfo.InvariantCulture)
                + "℃";
        }
    }
}
