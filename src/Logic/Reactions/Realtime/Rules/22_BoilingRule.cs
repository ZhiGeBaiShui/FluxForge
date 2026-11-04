using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 沸腾蒸发：基于原版 <c>ChemicalHelper.HandleBoilingReaction</c>，
    /// 蒸发量改为随过热度加速。
    ///
    /// 原版只有 <see cref="FlowBoilingTable"/> 里登记的液体（水 100、酒精 78.37、
    /// lipobond_agent 55）会蒸发；温度严格高于沸点才处理，每轮固定移除 2%。
    /// 这里保留「沸点开关」，把固定 2% 换成按过热度加速：
    ///
    ///   rate = 2% × 2^((T − 沸点) / 20)
    ///
    /// 20 度翻倍的斜率取自水的 Antoine 方程（<c>log10 P = A − B/(C+T)</c>）：
    /// 蒸气压在 100→120°C 涨 1.95 倍、120→140°C 涨 2.2 倍，与「2 倍 / 20 度」
    /// 几乎重合，但不必为每种液体单独维护 Antoine 系数。
    ///
    /// 与原版的差异只有一处：原版沸点以上恒为 2%，温度再高也不变；
    /// 这里温度越高蒸发越快，120°C 是 4%，140°C 是 8%。
    /// </summary>
    public sealed class BoilingRule : FlowReactionRule
    {
        /// <summary>沸点处的每轮蒸发比例（与原版一致）。</summary>
        private const double BaseEvaporationRate = 0.02d;

        /// <summary>过热度每高这么多度，蒸发速率翻倍（Antoine 曲线的指数近似）。</summary>
        private const double TemperatureDoubleStep = 20.0d;

        /// <summary>单轮蒸发下限（µL），与原版一致。</summary>
        private const int MinimumEvaporation = 100;

        public override int Order
        {
            get { return 220; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            IReadOnlyList<KeyValuePair<FlowPropertyKey, double>> entries = FlowBoilingTable.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                KeyValuePair<FlowPropertyKey, double> entry = entries[i];
                double boilingPoint = entry.Value;

                // 沸点开关：与原版一致，严格大于才蒸发。
                if (!(temperature > boilingPoint))
                {
                    continue;
                }

                int originalAmount = FlowReactionMath.GetInt(properties, entry.Key);
                if (originalAmount <= 0)
                {
                    continue;
                }

                double rate = EvaporationRateAt(temperature, boilingPoint);
                int amountToRemove = (int)((double)originalAmount * rate);
                amountToRemove = FlowReactionMath.Max(amountToRemove, MinimumEvaporation);
                if (amountToRemove > originalAmount)
                {
                    amountToRemove = originalAmount;
                }

                FlowReactionMath.SetInt(
                    properties,
                    entry.Key,
                    originalAmount - amountToRemove);
            }
        }

        /// <summary>
        /// 给定温度与该液体沸点，返回本轮蒸发比例。
        ///
        /// 沸点处 = <see cref="BaseEvaporationRate"/>；过热度每 +20 度翻倍；
        /// 上限 100%（整份蒸发，避免 rate &gt; 1 时算出负存量）。
        /// 温度不高于沸点时返回 0。
        /// </summary>
        public static double EvaporationRateAt(double temperature, double boilingPoint)
        {
            double overheat = temperature - boilingPoint;
            if (!(overheat > 0d))
            {
                return 0d;
            }

            double rate = BaseEvaporationRate * Math.Pow(2d, overheat / TemperatureDoubleStep);
            return rate > 1d ? 1d : rate;
        }
    }
}
