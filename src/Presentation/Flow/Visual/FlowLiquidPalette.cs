using System.Collections.Generic;

namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 36 种已注册液体 + 1 个外来液体兜底色。
    ///
    /// **色值来源与改动**：基础色取自游戏 <c>Liquid.InitLiquid()</c> 的 <c>color</c> 字段，
    /// 但**统一提过一轮饱和度**（HLS 空间：S × 2.85 上限 1.0，L &lt; 0.55 时向 0.55 靠 30%，
    /// 色相不动）。原因：原版色值整体偏灰，纯液体看起来和混合物没区别，
    /// 提饱和后「纯水 / 纯酸 / 纯氰化物」一眼就能认出是高纯度。
    ///
    /// 混合时不需要任何额外机制——不同色相按存量加权平均会自然去饱和，
    /// 所以「越混越灰、越纯越艳」是加权平均的天然结果，不是运行时调出来的。
    ///
    /// 只做「id → 颜色」的静态映射与染色权重，不参与混合；
    /// 混合由 <see cref="FlowVisualLayer"/> 按各液体存量加权完成。
    ///
    /// 顺序与 <c>FlowPropertyRegistry</c> 的液体槽位一致，改这里必须同步改注册表。
    /// </summary>
    public static class FlowLiquidPalette
    {
        /// <summary>没有可靠成分时的兜底色：水的本体色。</summary>
        public static readonly FlowColor Default = new FlowColor(0x2B, 0xE8, 0xDA);

        private static readonly Dictionary<string, FlowColor> Colors =
            new Dictionary<string, FlowColor>
            {
                { "water",                 new FlowColor(0x2B, 0xE8, 0xDA) },
                { "heavy_metal",           new FlowColor(0x4E, 0x55, 0x6B) },
                { "organic_waste",         new FlowColor(0xEA, 0x3B, 0x07) },
                { "microplastic",          new FlowColor(0x4E, 0x55, 0x6B) },
                { "microbe",               new FlowColor(0x00, 0xDA, 0x06) },
                { "chemical_contaminant",  new FlowColor(0x71, 0xFF, 0x0A) },
                { "physical_contaminant",  new FlowColor(0x4E, 0x55, 0x6B) },
                { "mineral",               new FlowColor(0x4E, 0x55, 0x6B) },
                { "dream_cap_mush",        new FlowColor(0xEA, 0x3B, 0x07) },
                { "dream_cap_extract",     new FlowColor(0xFF, 0x81, 0x21) },
                { "plant_matter",          new FlowColor(0x75, 0x8F, 0x27) },
                { "alcohol",               new FlowColor(0xFF, 0xFF, 0xFF) },
                { "caffeine",              new FlowColor(0xC1, 0x2E, 0x1E) },
                { "protein",               new FlowColor(0x2B, 0xE8, 0xDA) },
                { "fat",                   new FlowColor(0x2B, 0xE8, 0xDA) },
                { "starch",                new FlowColor(0x2B, 0xE8, 0xDA) },
                { "sugar",                 new FlowColor(0x2B, 0xE8, 0xDA) },
                { "coffee_mush",           new FlowColor(0x8A, 0x1F, 0x43) },
                { "contraceptive",         new FlowColor(0x2B, 0xE8, 0xDA) },
                { "zyanide",               new FlowColor(0x00, 0xAC, 0xA4) },
                { "nightmare_extract",     new FlowColor(0xE8, 0x00, 0x02) },
                { "nightmare_cap_mush",    new FlowColor(0xC8, 0x00, 0x1E) },
                { "chlorine",              new FlowColor(0x9E, 0xF8, 0x06) },
                { "synaprest",             new FlowColor(0x8C, 0x7C, 0x6A) },
                { "fanta",                 new FlowColor(0xC5, 0x3C, 0xE2) },
                { "methylol",              new FlowColor(0x00, 0x61, 0xC8) },
                { "reducing_agent",        new FlowColor(0x2B, 0xE8, 0xDA) },
                { "acid",                  new FlowColor(0x71, 0xFF, 0x0A) },
                { "base",                  new FlowColor(0x2B, 0xE8, 0xDA) },
                { "stabilizing_agent",     new FlowColor(0x59, 0xF0, 0xE5) },
                { "debris",                new FlowColor(0x4E, 0x55, 0x6B) },
                { "lipobond_agent",        new FlowColor(0xFF, 0x61, 0x22) },
                { "livestock_steroid",     new FlowColor(0xFF, 0xCE, 0x08) },
                { "livestock_immunity",    new FlowColor(0x08, 0xCE, 0xFF) },
                { "wine_extract",          new FlowColor(0xD6, 0x19, 0xE2) },
                { "nomore",                new FlowColor(0xFF, 0x66, 0xB3) },
                // 未注册液体的统一代表色：只用于显示，装置不处理。
                { "foreign",               new FlowColor(0x9B, 0x7B, 0xAA) },
            };

        /// <summary>
        /// 染色权重系数，对应游戏 <c>Liquid.colorMultiplier</c> 字段。
        ///
        /// 原版 <c>WaterHelper.GetLiquidContainerColor</c> 的权重是
        /// <c>amount × Mathf.Max(1, colorMultiplier)</c>；36 种液体里只有 6 个不等于 1，
        /// 其余全部是 1（也就是「按存量等比混合」）。这里只登记非 1 的，
        /// 未登记的走 <see cref="GetColorMultiplier"/> 的默认值 1。
        ///
        /// ⚠ <c>nightmare_cap_mush</c> 在原版源码里是 1，不是 CSV 记的 4：
        /// <c>Liquid.InitLiquid</c> 的 liquid22 初始化块里误写成
        /// <c>liquid21.colorMultiplier = 4;</c>（把 nightmare_extract 赋了两次），
        /// 所以运行时 nightmare_cap_mush 保持默认值 1。这里按运行值对齐。
        /// </summary>
        private static readonly Dictionary<string, double> ColorMultiplier =
            new Dictionary<string, double>
            {
                { "wine_extract",          150d }, // 1 单位酒提取 = 150 单位水的染色权重
                { "organic_waste",         7d },
                { "chemical_contaminant",  6d },
                { "heavy_metal",           4d },
                { "microbe",               4d },
                { "nightmare_extract",     4d },
            };

        /// <summary>
        /// 按液体 id 取染色权重；未登记返回 1。
        /// 与原版 <c>Mathf.Max(1, colorMultiplier)</c> 同义——原版用 Max 兜住
        /// 0 或负数，保证任何成分都不会被权重抹掉。
        /// </summary>
        public static double GetColorMultiplier(string id)
        {
            double value;
            if (!ColorMultiplier.TryGetValue(id, out value))
            {
                value = 1d;
            }

            return value < 1d ? 1d : value;
        }

        /// <summary>按液体 id 取本体颜色；未登记的 id 退回 <see cref="Default"/>。</summary>
        public static FlowColor Get(string id)
        {
            FlowColor color;
            return Colors.TryGetValue(id, out color) ? color : Default;
        }
    }
}
