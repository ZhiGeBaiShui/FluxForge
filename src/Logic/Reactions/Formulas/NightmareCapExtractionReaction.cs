namespace FluxForge
{
    /// <summary>
    /// 梦魇帽菇糊的酒精萃取。
    ///
    /// 简化模型：不再逐 tick 处理 10%，而是直接按当前菌糊量一次性转化。
    /// 转化率 = 50% × min(酒精 / (菌糊 × 4), 1)。
    /// 酒精到达菌糊的 4 倍时达到 50% 上限；未达到时按比例降低。
    /// 酒精只作为比例条件，不消耗。
    /// </summary>
    public static class NightmareCapExtractionReaction
    {
        private const double SaturationRatio = 4d;

        public static bool React(FlowProperties properties)
        {
            if (properties.Get(FlowProps.Temperature) < 40d)
            {
                return false;
            }

            double mush = properties.Get(FlowProps.NightmareCapMush);
            double alcohol = properties.Get(FlowProps.Alcohol);
            if (mush <= 0d || alcohol <= 0d)
            {
                return false;
            }

            double ratio = alcohol / (mush * SaturationRatio);
            double yield = 0.5d * (ratio < 1d ? ratio : 1d);
            double extract = System.Math.Round(mush * yield);
            double plant = mush - extract;

            properties.Set(FlowProps.NightmareCapMush, 0d);
            properties.Set(
                FlowProps.NightmareExtract,
                properties.Get(FlowProps.NightmareExtract) + extract);
            properties.Set(
                FlowProps.PlantMatter,
                properties.Get(FlowProps.PlantMatter) + plant);
            return true;
        }
    }
}
