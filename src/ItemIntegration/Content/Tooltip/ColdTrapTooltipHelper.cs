using System;
using System.Globalization;

namespace FluxForge.Mod
{
    /// <summary>
    /// 冷萃装置专用 tooltip：只显示当前收集到的总物质量，不显示容量。
    /// 低于 1 mL 时用 µL，达到 1 mL 后用 mL。
    /// </summary>
    public sealed class ColdTrapTooltipHelper : IFlowTooltipHelper
    {
        private const string DeviceKey = "cold_trap";

        public void Append(RichTextBuilder builder, GameItemElement item)
        {
            FlowItemDefinition def = FlowItemRegistry.Instance.Get(item.identifier);
            if (def == null || def.device != DeviceKey)
            {
                return;
            }

            double microlitres = FlowStock.Total(item);
            builder.AddLine(
                ModHelper.GetLocalized(ItemFactory.ModId, "tooltip.collected_amount", Format(microlitres)),
                true,
                RenderHandler.ColorPalette.LightBlue);
        }

        private static string Format(double microlitres)
        {
            if (microlitres < 1000d)
            {
                int value = (int)Math.Floor(microlitres);
                return value.ToString(CultureInfo.InvariantCulture) + " µL";
            }

            double millilitres = microlitres / 1000d;
            return millilitres.ToString("0.##", CultureInfo.InvariantCulture)
                + " mL";
        }
    }
}
