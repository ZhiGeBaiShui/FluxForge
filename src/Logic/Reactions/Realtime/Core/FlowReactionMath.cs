using System;

namespace FluxForge
{
    /// <summary>
    /// 原版容器反应使用 int µL；流源属性是 double，但移植公式时在计算点
    /// 显式还原原版的截断 / 四舍五入，避免直接 double 化造成语义漂移。
    /// </summary>
    public static class FlowReactionMath
    {
        public static int GetInt(FlowProperties properties, FlowPropertyKey key)
        {
            return (int)properties.Get(key);
        }

        /// <summary>原版 WaterHelper.GetTotalVolume 的 int 总量语义。</summary>
        public static int TotalInt(FlowProperties properties)
        {
            return (int)properties.SubstanceTotal;
        }

        public static void SetInt(FlowProperties properties, FlowPropertyKey key, int value)
        {
            properties.Set(key, value);
        }

        public static int Round(double value)
        {
            return (int)Math.Round(value);
        }

        public static int Ceiling(double value)
        {
            return (int)Math.Ceiling(value);
        }

        public static int Min(int left, int right)
        {
            return left < right ? left : right;
        }

        public static int Max(int left, int right)
        {
            return left > right ? left : right;
        }
    }
}
