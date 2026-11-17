using System;

namespace FluxForge
{
    /// <summary>
    /// 过滤反应公式：只去除 chemical_contaminant 与 heavy_metal。
    ///
    /// 工具类，不是装置；由过滤装置在流源穿过时调用。
    ///
    /// **温度决定污染占比下限，单次穿过直接降到该下限**：
    /// <code>
    /// floor = 1.01% − (T − 100) × 0.01%
    /// </code>
    /// 100 度 = 1.01%，120 度 = 0.81%，191 度 = 0.10%（纯水线），
    /// 201 度及以上 = 0（完全无污染）。低于 100 度时下限更高，
    /// 所以「降到 1% 以下」必须加热。
    ///
    /// 单次达到下限需要解一个占比方程：
    /// <code>
    /// target = floor × (总量 − 当前污染) / (1 − floor)
    /// </code>
    /// 这样去除后 <c>target / (剩余液体 + target)</c> 正好等于 floor，
    /// 而不是只把污染绝对量压到 floor × 原总量。
    /// </summary>
    public static class FilterReaction
    {
        /// <summary>100 度时的污染占比下限：1.01%（对应纯度 98.99%）。</summary>
        public const double FloorAtBoilingPoint = 0.0101d;

        /// <summary>温度每升高 1 度，下限下降的比例：0.01%。</summary>
        public const double FloorDropPerDegree = 0.0001d;

        /// <summary>
        /// 判断是否已经到达下限的相对容差。第二次穿过时 target 与 contaminants
        /// 可能只差 double 舍入误差（实测约 9e-13），直接比较会把残差当成
        /// 一次新的去除并再次播报音效。
        /// </summary>
        private const double FloorComparisonEpsilon = 1e-9d;

        /// <summary>温度基准点（水的沸点）。</summary>
        public const double ReferenceTemperature = 100d;

        /// <summary>
        /// 给定温度下的污染占比下限（相对 <see cref="FlowProperties.SubstanceTotal"/>）。
        /// 100 度 = 0.0101，120 度 = 0.0081，191 度 = 0.001，200 度 = 0.0001，
        /// 201 度以上 = 0。
        /// </summary>
        public static double ContaminantFloorAt(double temperature)
        {
            double floor = FloorAtBoilingPoint
                - (temperature - ReferenceTemperature) * FloorDropPerDegree;

            return floor > 0d ? floor : 0d;
        }

        /// <summary>
        /// 一次穿过就把化学污染物与重金属降到当前温度支持的下限。
        ///
        /// 两种污染物按各自存量比例分配去除量，相对比例保持不变。
        /// 温度下限 ≥ 100% 时不处理，因为不存在可达到的更低污染占比。
        /// </summary>
        public static void React(FlowProperties properties)
        {
            double chemical = properties.Get(FlowProps.ChemicalContaminant);
            double heavyMetal = properties.Get(FlowProps.HeavyMetal);
            double contaminants = chemical + heavyMetal;
            if (contaminants <= 0d)
            {
                return;
            }

            double floor = ContaminantFloorAt(
                properties.Get(FlowProps.Temperature));
            if (floor >= 1d)
            {
                return;
            }

            double nonContaminants = properties.SubstanceTotal - contaminants;
            double target = floor * nonContaminants / (1d - floor);
            double epsilon = contaminants * FloorComparisonEpsilon;
            if (target >= contaminants - epsilon)
            {
                return;
            }

            double removed = contaminants - target;
            double chemicalRemoved = removed * (chemical / contaminants);
            properties.Set(
                FlowProps.ChemicalContaminant,
                chemical - chemicalRemoved);
            properties.Set(
                FlowProps.HeavyMetal,
                heavyMetal - (removed - chemicalRemoved));
        }
    }
}
