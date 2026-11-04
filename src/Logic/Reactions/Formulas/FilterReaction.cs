using System;

namespace FluxForge
{
    /// <summary>
    /// 过滤反应公式：只去除 chemical_contaminant 与 heavy_metal。
    ///
    /// 工具类，不是装置；由过滤装置在流源穿过时调用，speed 由装置提供。
    ///
    /// 两个维度各管一件事：
    ///   · **温度决定下限**——污染占比最低能降到多少；
    ///   · **速度决定单次比例**——这一趟能带走多少。
    ///
    /// 温度下限（占总液体量的比例）：
    /// <code>
    /// floor = 1.01% − (T − 100) × 0.01%
    /// </code>
    /// 100 度 = 1.01%，120 度 = 0.81%，191 度 = 0.10%（纯水线），
    /// 201 度及以上 = 0（完全无污染）。低于 100 度时下限更高，
    /// 所以「降到 1% 以下」必须加热。
    ///
    /// 单次比例（平方反比，低速趋近 80%，高速趋近 0）：
    /// <code>
    /// fraction = 0.8 / (1 + speed/10)²
    /// </code>
    /// speed = 2.5 时 51.2%，speed = 5 时 35.6%，speed = 10 时 20%。
    /// 平方反比保证「堆最高速」不划算：速度翻 10 倍，单次比例掉到约 1/40。
    /// </summary>
    public static class FilterReaction
    {
        /// <summary>100 度时的污染占比下限：1.01%（对应纯度 98.99%）。</summary>
        public const double FloorAtBoilingPoint = 0.0101d;

        /// <summary>温度每升高 1 度，下限下降的比例：0.01%。</summary>
        public const double FloorDropPerDegree = 0.0001d;

        /// <summary>温度基准点（水的沸点）。</summary>
        public const double ReferenceTemperature = 100d;

        /// <summary>速度基准：speed = 10 时单次比例降到四分之一。</summary>
        public const double SpeedScale = 10d;

        /// <summary>
        /// 低速基准比例：<c>speed → 0</c> 时单次去除比例趋近此值。
        ///
        /// 因为去除量乘的是「超出下限的余量」而不是污染总量，这个系数比
        /// 「按总量去除」时更大：speed = 2.5 时 51.2%，speed = 10 时 20%。
        ///
        /// 取值经过平衡（2000ml、2% 基础水、120 度、目标优质水 1%）：
        ///   · 4 格循环 @2.5：5 圈、8 秒、损耗 27.9%，符合「前期 30% 损耗」目标；
        ///   · 直线串联：5 个反应装置 + 4 辅助格，损耗 13.7%。
        /// 直线更省水但要多造 4 个反应装置，循环用 1 个反应装置 + 4 个转向装置反复利用。
        /// </summary>
        public const double BaseRemovalFraction = 0.8d;

        /// <summary>
        /// 给定温度下的污染占比下限（相对 <see cref="FlowProperties.SubstanceTotal"/>）。
        /// 100 度 = 0.0101，120 度 = 0.0081，191 度 = 0.001，201 度以上 = 0。
        /// </summary>
        public static double ContaminantFloorAt(double temperature)
        {
            double floor = FloorAtBoilingPoint
                - (temperature - ReferenceTemperature) * FloorDropPerDegree;

            return floor > 0d ? floor : 0d;
        }

        /// <summary>
        /// 给定速度下的单次去除比例：<c>0.8 / (1 + speed/10)²</c>。
        ///
        /// 平方反比让「堆最高速」不再划算——speed = 100 时单次只剩 0.66%，
        /// 需要几百圈才能补回来，总耗时和蒸发量都超过中速。
        /// speed ≤ 0 返回 0：流源不动就不该有「穿过装置」这回事。
        /// </summary>
        public static double RemovalFractionAt(double speed)
        {
            if (!(speed > 0d))
            {
                return 0d;
            }

            double decay = 1d + speed / SpeedScale;
            return BaseRemovalFraction / (decay * decay);
        }

        /// <summary>
        /// 按温度下限与速度比例去除化学污染物与重金属；其它液体保持不变。
        ///
        /// 去除量按**超出下限的余量**计算，不是按污染总量：
        /// <code>
        /// headroom  = total − floor × SubstanceTotal
        /// removable = headroom × fraction
        /// </code>
        /// 因为 <c>fraction &lt; 1</c>，removable 永远小于余量，所以不需要额外钳制——
        /// 越接近下限可去除的越少，自然形成平滑渐近。玩家能感觉到「越接近极限越难降」，
        /// 而不是撞上一道硬墙。
        ///
        /// 两种污染物按各自存量比例分配去除量，相对比例保持不变。
        /// </summary>
        public static void React(FlowProperties properties, double speed)
        {
            double chemical = properties.Get(FlowProps.ChemicalContaminant);
            double heavyMetal = properties.Get(FlowProps.HeavyMetal);
            double total = chemical + heavyMetal;
            if (total <= 0d)
            {
                return;
            }

            double temperature = properties.Get(FlowProps.Temperature);
            double floor = ContaminantFloorAt(temperature) * properties.SubstanceTotal;

            double headroom = total - floor;
            if (headroom <= 0d)
            {
                return;
            }

            double removable = headroom * RemovalFractionAt(speed);
            if (removable <= 0d)
            {
                return;
            }

            double chemicalRemoved = removable * (chemical / total);
            properties.Set(FlowProps.ChemicalContaminant, chemical - chemicalRemoved);
            properties.Set(FlowProps.HeavyMetal, heavyMetal - (removable - chemicalRemoved));
        }
    }
}
