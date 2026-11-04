using System;

namespace FluxForge
{
    /// <summary>
    /// 过滤反应公式：只去除 chemical_contaminant 与 heavy_metal。
    ///
    /// 工具类，不是装置；由未来的过滤装置在接触流源时调用，参数由装置提供。
    ///
    /// 温度决定“最多能净化到什么程度”，速度决定“单次最多带走多少”：
    ///   removalFraction = 0.25 + 0.73 × clamp(T / 100, 0, 1)
    ///   capacity        = 1000 / (1 + max(speed, 0) / 10)
    ///   removable       = min(total × removalFraction, capacity)
    ///
    /// total 只计算 chemical_contaminant + heavy_metal，两者按存量比例分配。
    /// </summary>
    public static class FilterReaction
    {
        public const double MinTemperature = 0d;
        public const double MaxTemperature = 100d;
        public const double MinRemovalFraction = 0.25d;
        public const double MaxRemovalFraction = 0.98d;
        public const double BaseCapacity = 1000d;
        public const double SpeedHalfCapacity = 10d;

        /// <summary>给定温度下允许的最大去除比例。</summary>
        public static double RemovalFractionAt(double temperature)
        {
            double normalized = (temperature - MinTemperature)
                / (MaxTemperature - MinTemperature);
            if (normalized < 0d)
            {
                normalized = 0d;
            }
            else if (normalized > 1d)
            {
                normalized = 1d;
            }

            return MinRemovalFraction
                + (MaxRemovalFraction - MinRemovalFraction) * normalized;
        }

        /// <summary>给定速度下的单次总容量（µL）。</summary>
        public static double CapacityAt(double speed)
        {
            double positiveSpeed = speed > 0d ? speed : 0d;
            return BaseCapacity / (1d + positiveSpeed / SpeedHalfCapacity);
        }

        /// <summary>
        /// 按温度/速度去除化学污染物与重金属；其它液体保持不变。
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
            double removableFraction = RemovalFractionAt(temperature);
            double capacity = CapacityAt(speed);

            double removable = total * removableFraction;
            if (removable > capacity)
            {
                removable = capacity;
            }

            if (removable <= 0d)
            {
                return;
            }

            double chemicalRemoved = removable * (chemical / total);
            double heavyMetalRemoved = removable - chemicalRemoved;

            properties.Set(FlowProps.ChemicalContaminant, chemical - chemicalRemoved);
            properties.Set(FlowProps.HeavyMetal, heavyMetal - heavyMetalRemoved);
        }
    }
}
