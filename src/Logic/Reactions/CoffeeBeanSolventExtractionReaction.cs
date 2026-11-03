namespace FluxForge
{
    /// <summary>
    /// 咖啡豆糊的酒精萃取。
    ///
    /// 简化模型：不再逐 tick 处理 15%，而是直接按当前咖啡豆糊量一次性转化。
    /// 转化率 = 75% × min(酒精 / (咖啡豆糊 × 0.75), 1)。
    /// 酒精到达咖啡豆糊的 0.75 倍时达到 75% 上限；未达到时按比例降低。
    /// 酒精只作为比例条件，不消耗。
    /// </summary>
    public static class CoffeeBeanSolventExtractionReaction
    {
        private const double SaturationRatio = 0.75d;

        public static bool React(FlowProperties properties)
        {
            if (properties.Get(FlowProps.Temperature) < 50d)
            {
                return false;
            }

            double mush = properties.Get(FlowProps.CoffeeMush);
            double alcohol = properties.Get(FlowProps.Alcohol);
            if (mush <= 0d || alcohol <= 0d)
            {
                return false;
            }

            double ratio = alcohol / (mush * SaturationRatio);
            double yield = 0.75d * (ratio < 1d ? ratio : 1d);
            double caffeine = System.Math.Round(mush * yield);
            double plant = mush - caffeine;

            properties.Set(FlowProps.CoffeeMush, 0d);
            properties.Set(
                FlowProps.Caffeine,
                properties.Get(FlowProps.Caffeine) + caffeine);
            properties.Set(
                FlowProps.PlantMatter,
                properties.Get(FlowProps.PlantMatter) + plant);
            return true;
        }
    }
}
