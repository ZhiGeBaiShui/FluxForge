using System.Collections.Generic;

namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// 36 种液体的本体颜色表，来源为游戏 <c>Liquid.InitLiquid()</c> 的 <c>color</c> 字段。
    ///
    /// 只做「id → 颜色」的静态映射与稀有度权重，不参与混合；
    /// 混合由 <see cref="FlowTint.Body"/> 的调用方按各液体存量加权完成。
    ///
    /// 顺序与 <c>FlowPropertyRegistry</c> 的液体槽位一致，改这里必须同步改注册表。
    /// </summary>
    public static class FlowLiquidPalette
    {
        /// <summary>没有可靠成分时的兜底色：水的本体色。</summary>
        public static readonly FlowColor Default = new FlowColor(0x67, 0xAA, 0xA5);

        private static readonly Dictionary<string, FlowColor> Colors =
            new Dictionary<string, FlowColor>
            {
                { "water",                 new FlowColor(0x67, 0xAA, 0xA5) },
                { "heavy_metal",           new FlowColor(0x44, 0x46, 0x4C) },
                { "organic_waste",         new FlowColor(0x95, 0x5C, 0x4B) },
                { "microplastic",          new FlowColor(0x44, 0x46, 0x4C) },
                { "microbe",               new FlowColor(0x3D, 0x82, 0x3F) },
                { "chemical_contaminant",  new FlowColor(0x7A, 0xB0, 0x53) },
                { "physical_contaminant",  new FlowColor(0x44, 0x46, 0x4C) },
                { "mineral",               new FlowColor(0x44, 0x46, 0x4C) },
                { "dream_cap_mush",        new FlowColor(0x95, 0x5C, 0x4B) },
                { "dream_cap_extract",     new FlowColor(0xBB, 0x8A, 0x65) },
                { "plant_matter",          new FlowColor(0x4D, 0x54, 0x38) },
                { "alcohol",               new FlowColor(0xFF, 0xFF, 0xFF) },
                { "caffeine",              new FlowColor(0x7D, 0x4F, 0x4A) },
                { "protein",               new FlowColor(0x67, 0xAA, 0xA5) },
                { "fat",                   new FlowColor(0x67, 0xAA, 0xA5) },
                { "starch",                new FlowColor(0x67, 0xAA, 0xA5) },
                { "sugar",                 new FlowColor(0x67, 0xAA, 0xA5) },
                { "coffee_mush",           new FlowColor(0x4A, 0x2F, 0x38) },
                { "contraceptive",         new FlowColor(0x67, 0xAA, 0xA5) },
                { "zyanide",               new FlowColor(0x27, 0x56, 0x54) },
                { "nightmare_extract",     new FlowColor(0x9C, 0x37, 0x38) },
                { "nightmare_cap_mush",    new FlowColor(0x74, 0x31, 0x3B) },
                { "chlorine",              new FlowColor(0x84, 0xA2, 0x51) },
                { "synaprest",             new FlowColor(0x79, 0x74, 0x6E) },
                { "fanta",                 new FlowColor(0xA2, 0x72, 0xAC) },
                { "methylol",              new FlowColor(0x35, 0x52, 0x71) },
                { "reducing_agent",        new FlowColor(0x67, 0xAA, 0xA5) },
                { "acid",                  new FlowColor(0x7A, 0xB0, 0x53) },
                { "base",                  new FlowColor(0x67, 0xAA, 0xA5) },
                { "stabilizing_agent",     new FlowColor(0x8A, 0xBF, 0xBB) },
                { "debris",                new FlowColor(0x44, 0x46, 0x4C) },
                { "lipobond_agent",        new FlowColor(0xCC, 0x77, 0x55) },
                { "livestock_steroid",     new FlowColor(0xFF, 0xCC, 0x00) },
                { "livestock_immunity",    new FlowColor(0x00, 0xCC, 0xFF) },
                { "wine_extract",          new FlowColor(0x95, 0x56, 0x99) },
                { "nomore",                new FlowColor(0xD9, 0x8C, 0xB3) },
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
