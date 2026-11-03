using System;
using System.Collections.Generic;
using System.Globalization;

namespace FluxForge.Mod
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
        private const RenderHandler.ColorPalette DefaultColor
            = RenderHandler.ColorPalette.LightBlue;

        /// <summary>
        /// 颜色名 → 调色板缓存。tooltip 每帧全量重建，Enum.Parse + try/catch 不能放在热路径上；
        /// 缓存键只来自 items.json 的 statusLines，数量有界。
        /// </summary>
        private static readonly Dictionary<string, RenderHandler.ColorPalette> ColorCache
            = new Dictionary<string, RenderHandler.ColorPalette>(StringComparer.Ordinal);

        /// <summary>已经警告过的未知颜色名，避免每帧刷日志。</summary>
        private static readonly HashSet<string> WarnedColors
            = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// 追加本物品在 items.json 里声明的状态行。
        ///
        /// 前置条件：<paramref name="builder"/> 与 <paramref name="item"/> 均非 null；
        /// 唯一调用入口 <see cref="FlowTooltipPatch.Postfix"/> 在分派前已经判过，
        /// 所以这里不再重复检查。
        /// </summary>
        public void Append(RichTextBuilder builder, GameItemElement item)
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

        private static void AppendLine(RichTextBuilder builder, GameItemElement item, StatusLine line)
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

            TagState state = item.GetTagReadonly(line.tag);
            if (state == null)
            {
                return;
            }

            string text = line.label + "：" + FormatValue(state, line) + (line.unit ?? string.Empty);
            builder.AddLine(text, true, ParseColor(line.color));
        }

        private static string FormatValue(TagState state, StatusLine line)
        {
            string format = string.IsNullOrEmpty(line.format) ? DefaultFormat : line.format;
            double scale = line.scale > 0d ? line.scale : 1d;

            // 用 OrdinalIgnoreCase 比较：valueType 是配置里的固定小写词，
            // 每次 ToLowerInvariant 会多一次分配，而这里在 tooltip 热路径上。
            if (string.Equals(line.valueType, "string", StringComparison.OrdinalIgnoreCase))
            {
                return state.GetString() ?? string.Empty;
            }

            if (string.Equals(line.valueType, "double", StringComparison.OrdinalIgnoreCase))
            {
                return (state.GetDouble() * scale).ToString(format, CultureInfo.InvariantCulture);
            }

            return (state.GetInt() * scale).ToString(format, CultureInfo.InvariantCulture);
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
                    FFModEntry.Log.Warning("[tooltip] 未知颜色名：" + name + "（用默认 LightBlue）");
                }

                parsed = DefaultColor;
            }

            ColorCache[name] = parsed;
            return parsed;
        }
    }
}
