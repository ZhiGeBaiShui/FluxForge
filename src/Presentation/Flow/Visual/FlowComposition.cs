using System;

namespace ProbablyBagMod.Presentation.Flow.Visual
{
    /// <summary>
    /// 流源的六分量成分（相对权重，**不要求调用方先归一化**）。
    ///
    /// 视觉契约见 <c>REFACTOR-A.md</c> §3：
    ///   · <see cref="Blend"/> 按 <c>Σ(w_i · comp_i) / Σw_i</c> 归一化加权，得到本体色；
    ///   · 全 0 或含 NaN 视为「没有可靠成分数据」，退到纯水色 <c>#27C7E6</c>；
    ///   · 六种基础色与 <c>render_final.py</c> 的 <c>COMPS</c> 表逐字节一致，改这里必须同步改渲染脚本。
    ///
    /// 纯值类型、零 Unity 依赖，可在测试项目里直接构造。
    /// </summary>
    public readonly struct FlowComposition
    {
        // 六种基础色（render_final.COMPS）。顺序必须与字段顺序一致。
        private static readonly FlowColor WaterColor = new FlowColor(0x27, 0xC7, 0xE6);
        private static readonly FlowColor PoisonColor = new FlowColor(0x9B, 0x5C, 0xFF);
        private static readonly FlowColor OilColor = new FlowColor(0xF0, 0xB3, 0x2B);
        private static readonly FlowColor BloodColor = new FlowColor(0xE0, 0x3A, 0x56);
        private static readonly FlowColor AcidColor = new FlowColor(0x7F, 0xD1, 0x2B);
        private static readonly FlowColor EnergyColor = new FlowColor(0xE9, 0xF2, 0xFF);

        /// <summary>水权重（纯水 = 1，其余 0）。</summary>
        public readonly float Water;

        /// <summary>毒权重。</summary>
        public readonly float Poison;

        /// <summary>油权重。</summary>
        public readonly float Oil;

        /// <summary>血权重。</summary>
        public readonly float Blood;

        /// <summary>酸权重。</summary>
        public readonly float Acid;

        /// <summary>能量权重。</summary>
        public readonly float Energy;

        /// <summary>按「水、毒、油、血、酸、能量」的顺序构造成分。</summary>
        public FlowComposition(float water, float poison, float oil, float blood, float acid, float energy)
        {
            Water = water;
            Poison = poison;
            Oil = oil;
            Blood = blood;
            Acid = acid;
            Energy = energy;
        }

        /// <summary>纯水：唯一一个「有数据且确定」的默认成分。</summary>
        public static FlowComposition PureWater
        {
            get { return new FlowComposition(1f, 0f, 0f, 0f, 0f, 0f); }
        }

        /// <summary>
        /// 是否没有可用的成分数据：全部为 0，或任一通道是 NaN。
        /// 这两种情况都按「未知」处理，染色退到纯水色，而不是把 NaN 带进像素运算。
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                if (float.IsNaN(Water) || float.IsNaN(Poison) || float.IsNaN(Oil) ||
                    float.IsNaN(Blood) || float.IsNaN(Acid) || float.IsNaN(Energy))
                {
                    return true;
                }

                return Water == 0f && Poison == 0f && Oil == 0f &&
                       Blood == 0f && Acid == 0f && Energy == 0f;
            }
        }

        /// <summary>
        /// 归一化加权混合六种基础色：<c>Σ(w_i · comp_i) / Σw_i</c>。
        ///
        /// 空成分（全 0 / 含 NaN）返回水色 <c>#27C7E6</c>。
        /// 权重和 ≤ 0 或非有限值（脏数据）也返回水色，保证永远不会产生 NaN 像素。
        /// 每通道按 <see cref="Math.Round(double)"/>（银行家舍入，恰为 .5 时取偶）落到 byte，
        /// 与 <c>render_final.py</c> 里 <c>round()</c> 的舍入行为一致。
        /// </summary>
        public FlowColor Blend()
        {
            if (IsEmpty)
            {
                return WaterColor;
            }

            double total = (double)Water + Poison + Oil + Blood + Acid + Energy;
            if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0d)
            {
                return WaterColor;
            }

            double red = ((double)Water * WaterColor.R +
                         (double)Poison * PoisonColor.R +
                         (double)Oil * OilColor.R +
                         (double)Blood * BloodColor.R +
                         (double)Acid * AcidColor.R +
                         (double)Energy * EnergyColor.R) / total;

            double green = ((double)Water * WaterColor.G +
                           (double)Poison * PoisonColor.G +
                           (double)Oil * OilColor.G +
                           (double)Blood * BloodColor.G +
                           (double)Acid * AcidColor.G +
                           (double)Energy * EnergyColor.G) / total;

            double blue = ((double)Water * WaterColor.B +
                          (double)Poison * PoisonColor.B +
                          (double)Oil * OilColor.B +
                          (double)Blood * BloodColor.B +
                          (double)Acid * AcidColor.B +
                          (double)Energy * EnergyColor.B) / total;

            return new FlowColor(
                FlowColor.RoundToByte(red),
                FlowColor.RoundToByte(green),
                FlowColor.RoundToByte(blue));
        }
    }
}
