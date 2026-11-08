using System;
using System.Collections.Generic;
using System.Globalization;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置容器 tooltip：只处理带 <see cref="FlowTags.DeviceContainerTag"/> 的
    /// 自定义容器，按 items.json 的 statusLines 显示系数 / 存量 / 容量。
    ///
    /// 与 <see cref="StatusLineTooltipHelper"/> 分开：后者只服务温度装置，
    /// 这里不改变原版温度 tooltip 的行为。
    /// </summary>
    public sealed class DeviceContainerTooltipHelper : IFlowTooltipHelper
    {
        private const string DefaultFormat = "0.#";
        private const RenderHandler.ColorPalette DefaultColor
            = RenderHandler.ColorPalette.LightBlue;

        private static readonly Dictionary<string, RenderHandler.ColorPalette> ColorCache
            = new Dictionary<string, RenderHandler.ColorPalette>(StringComparer.Ordinal);

        private static readonly HashSet<string> WarnedColors
            = new HashSet<string>(StringComparer.Ordinal);

        public void Append(RichTextBuilder builder, GameItemElement item)
        {
            FlowItemDefinition def = FlowItemRegistry.Instance.Get(item.identifier);
            if (def == null
                || !item.IsTag(FlowTags.DeviceContainerTag)
                || def.statusLines == null
                || def.statusLines.Length == 0)
            {
                return;
            }

            for (int i = 0; i < def.statusLines.Length; i++)
            {
                StatusLine line = def.statusLines[i];
                if (line == null
                    || string.IsNullOrEmpty(line.tag)
                    || string.IsNullOrEmpty(line.label)
                    || !item.IsTag(line.tag))
                {
                    continue;
                }

                TagState state = item.GetTagReadonly(line.tag);
                if (state == null)
                {
                    continue;
                }

                string text = line.label
                    + "：" + FormatValue(state, line)
                    + (line.unit ?? string.Empty);

                builder.AddLine(text, true, ParseColor(line.color));
            }

            // 存量不落额外 tag：直接从 CURRENT_PART_* 实时求和。
            double millilitres = FlowStock.Total(item) / 1000d;
            builder.AddLine(
                "存量：" + millilitres.ToString(DefaultFormat, CultureInfo.InvariantCulture) + "mL",
                true,
                DefaultColor);
        }

        private static string FormatValue(TagState state, StatusLine line)
        {
            string format = string.IsNullOrEmpty(line.format)
                ? DefaultFormat
                : line.format;
            double scale = line.scale > 0d ? line.scale : 1d;

            if (string.Equals(line.valueType, "string", StringComparison.OrdinalIgnoreCase))
            {
                return state.GetString() ?? string.Empty;
            }

            if (string.Equals(line.valueType, "double", StringComparison.OrdinalIgnoreCase))
            {
                return (state.GetDouble() * scale)
                    .ToString(format, CultureInfo.InvariantCulture);
            }

            return (state.GetInt() * scale)
                .ToString(format, CultureInfo.InvariantCulture);
        }

        private static RenderHandler.ColorPalette ParseColor(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return DefaultColor;
            }

            RenderHandler.ColorPalette cached;
            if (ColorCache.TryGetValue(name, out cached))
            {
                return cached;
            }

            RenderHandler.ColorPalette parsed;
            try
            {
                parsed = (RenderHandler.ColorPalette)Enum.Parse(
                    typeof(RenderHandler.ColorPalette), name, true);
            }
            catch
            {
                if (WarnedColors.Add(name))
                {
                    FFModEntry.Log.Warning(
                        "[tooltip] 未知颜色名：" + name + "（用默认 LightBlue）");
                }

                parsed = DefaultColor;
            }

            ColorCache[name] = parsed;
            return parsed;
        }
    }
}
