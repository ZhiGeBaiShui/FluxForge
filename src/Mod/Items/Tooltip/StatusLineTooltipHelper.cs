using System;
using System.Globalization;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 通用状态行 helper：由 items.json 的 statusLines 声明驱动。
    ///
    /// 对应原版 WaterHelper.CreateWaterContainerTooltip 这类「自判 + 追加」的 helper，
    /// 只是判断条件不是硬编码 tag，而是「配置里有没有对这一项声明 statusLines」。
    /// 新增装置只改 json，不写代码。
    /// </summary>
    public sealed class StatusLineTooltipHelper : IFlowTooltipHelper
    {
        private const string DefaultFormat = "0.#";
        private const Il2Cpp.RenderHandler.ColorPalette DefaultColor
            = Il2Cpp.RenderHandler.ColorPalette.LightBlue;

        /// <summary>
        /// 追加本物品在 items.json 里声明的状态行。
        ///
        /// 前置条件：<paramref name="builder"/> 与 <paramref name="item"/> 均非 null；
        /// 唯一调用入口 <see cref="FlowTooltipPatch.Postfix"/> 在分派前已经判过，
        /// 所以这里不再重复检查。
        /// </summary>
        public void Append(Il2Cpp.RichTextBuilder builder, Il2Cpp.GameItemElement item)
        {
            // 自判：配置里没声明状态行 → 不归我管
            FlowItemDefinition def = FlowItemRegistry.Instance.Get(item.identifier);
            if (def == null || def.statusLines == null || def.statusLines.Length == 0)
            {
                return;
            }

            for (int i = 0; i < def.statusLines.Length; i++)
            {
                AppendLine(builder, item, def.statusLines[i]);
            }
        }

        private static void AppendLine(Il2Cpp.RichTextBuilder builder, Il2Cpp.GameItemElement item, StatusLine line)
        {
            if (line == null || string.IsNullOrEmpty(line.tag) || string.IsNullOrEmpty(line.label))
            {
                return;
            }

            // 自判：tag 不存在 → 这一项没数据，不显示
            if (!item.IsTag(line.tag))
            {
                return;
            }

            Il2Cpp.TagState state = item.GetTagReadonly(line.tag);
            if (state == null)
            {
                return;
            }

            string text = line.label + "：" + FormatValue(state, line) + (line.unit ?? string.Empty);
            builder.AddLine(text, true, ParseColor(line.color));
        }

        private static string FormatValue(Il2Cpp.TagState state, StatusLine line)
        {
            string format = string.IsNullOrEmpty(line.format) ? DefaultFormat : line.format;
            double scale = line.scale > 0d ? line.scale : 1d;

            string valueType = line.valueType == null ? "int" : line.valueType.ToLowerInvariant();

            if (valueType == "string")
            {
                return state.valueString ?? string.Empty;
            }

            if (valueType == "float")
            {
                return (state.valueFloat * scale).ToString(format, CultureInfo.InvariantCulture);
            }

            return (state.valueInt * scale).ToString(format, CultureInfo.InvariantCulture);
        }

        private static Il2Cpp.RenderHandler.ColorPalette ParseColor(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DefaultColor;
            }

            try
            {
                return (Il2Cpp.RenderHandler.ColorPalette)Enum.Parse(
                    typeof(Il2Cpp.RenderHandler.ColorPalette), name, true);
            }
            catch
            {
                MelonLogger.Warning("[tooltip] 未知颜色名：" + name + "（用默认 LightBlue）");
                return DefaultColor;
            }
        }
    }
}
