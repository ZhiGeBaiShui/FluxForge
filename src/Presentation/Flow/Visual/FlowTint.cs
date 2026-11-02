namespace FluxForge.Presentation.Flow.Visual
{
    /// <summary>
    /// A 方案（蒙版 + 运行时染色）的唯一调色入口，纯 C#、零 Unity。
    ///
    /// 与 <c>docs/flow-visual/final/render_final.py</c> 的 <c>edge_color</c>（第 69 行）
    /// 逐式对应：
    /// <code>
    /// base = shade(body, -0.52)
    /// cold → mix(base, #D6ECFF, 0.88)
    /// hot  → mix(base, #FF964E, 0.86)
    /// normal → base
    /// </code>
    /// 每一步都先落到 0–255 的 byte 再进入下一步，和 Python 侧返回整数元组的行为一致；
    /// 舍入统一走 <see cref="FlowColor.RoundToByte"/>（银行家舍入），以对齐 Python 的 <c>round()</c>。
    /// </summary>
    public static class FlowTint
    {
        // 温度染色目标（render_final.edge_color 里的两个字面量）。
        private static readonly FlowColor ColdTint = new FlowColor(0xD6, 0xEC, 0xFF);
        private static readonly FlowColor HotTint = new FlowColor(0xFF, 0x96, 0x4E);

        // shade 用的两个端点。
        private static readonly FlowColor White = new FlowColor(0xFF, 0xFF, 0xFF);
        private static readonly FlowColor Black = new FlowColor(0x00, 0x00, 0x00);

        /// <summary>本体颜色：直接取成分归一化混合的结果。</summary>
        public static FlowColor Body(in FlowComposition c)
        {
            return c.Blend();
        }

        /// <summary>
        /// 边缘（张力边 / 缝 / 干痕）颜色：先把本体压暗到 48%，再按温度向冷/热色混。
        /// 常温不混色；未知/越界温度档按常温处理。
        /// 这是 <c>render_final.edge_color</c> 的 C# 等价式，必须保持逐步 byte 舍入。
        /// </summary>
        public static FlowColor Edge(in FlowComposition c, FlowTemp temp)
        {
            FlowColor baseColor = Shade(Body(c), -0.52d);

            switch (temp)
            {
                case FlowTemp.Cold:
                    return FlowColor.Mix(baseColor, ColdTint, 0.88d);
                case FlowTemp.Hot:
                    return FlowColor.Mix(baseColor, HotTint, 0.86d);
                default:
                    return baseColor;
            }
        }

        /// <summary>
        /// 亮核 / 前脸高光 / 霜点 / 白沫共用的强调色：本体色向白混 62%。
        /// 与 <c>render_final.role_rgba</c> 的 <c>C</c> 角色逐字节一致（纯水 = <c>#ADEAF6</c>），
        /// 同族的 <c>G</c>(0.72) / <c>S</c>(0.60) / <c>~</c>(0.55) 也都落在这条白化曲线上，
        /// 因此油/酸的暖亮核不会再被固定近白 <c>#F2FAFF</c> 染成冷色。
        /// </summary>
        public static FlowColor Accent(in FlowComposition c)
        {
            return FlowColor.Mix(Body(c), White, 0.62d);
        }

        /// <summary>
        /// 明暗调整：向黑色混合 <paramref name="factor"/> 的比例。
        /// 与 <c>render_final.shade</c> 同式；唯一调用点恒传 -0.52（即每通道乘 0.48），
        /// 所以这里只保留「压暗」方向，不再处理 factor ≥ 0 的提亮分支。
        /// </summary>
        private static FlowColor Shade(FlowColor color, double factor)
        {
            return FlowColor.Mix(color, Black, -factor);
        }
    }
}
