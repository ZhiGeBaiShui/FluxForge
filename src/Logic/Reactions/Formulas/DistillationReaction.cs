using System;

namespace FluxForge
{
    /// <summary>
    /// 蒸馏只抽取水，不改变其它成分。机器传入资源和储水空间允许的抽取上限（µL），
    /// 返回实际抽取量，由机器保存并据此结算资源。反应后剩余液体含水率不低于 50%。
    /// </summary>
    public static class DistillationReaction
    {
        public static double React(FlowProperties properties, double maxWater)
        {
            double water = properties.Get(FlowProps.Water);
            double other = properties.SubstanceTotal - water;
            // 水不多于其它成分时，含水率已经不高于 50%，直接放行。
            // 纯水不蒸馏：抽取水无法让剩余纯水变成 50%，由机器原样输出且不扣资源。
            if (other <= 0d || water <= other || maxWater <= 0d)
                return 0d;

            double extracted = Math.Min(maxWater, water - other);
            properties.Set(FlowProps.Water, water - extracted);
            return extracted;
        }
    }
}
