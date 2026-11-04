namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleDreamCapExtractDegradation：梦帽萃取液的热降解（每 tick 一次）。
    ///
    /// 温度 &lt;60°C 不反应，且萃取液必须 &gt; 0。销毁比例按温度分段：
    ///   60–70°C ：0.1 × ((T − 60) / 10)，0 → 0.1
    ///   70–100°C：0.1 + 0.8 × ((T − 70) / 30)，0.1 → 0.9
    ///   ≥100°C  ：0.9 + 0.1 × min((T − 100) / 20, 1)，0.9 → 1
    /// 销毁量用 (int) 截断（不是 Math.Round）；只减少萃取液，不产生任何副产物。
    /// </summary>
    public sealed class DreamCapExtractDegradationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 90; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            int extract = FlowReactionMath.GetInt(properties, FlowProps.DreamCapExtract);
            if (extract <= 0)
            {
                return;
            }

            if (temperature < 60d)
            {
                return;
            }

            double fraction = 0d;
            if (temperature >= 60d && temperature < 70d)
            {
                fraction = 0.1d * ((temperature - 60d) / 10d);
            }
            else if (temperature >= 70d && temperature < 100d)
            {
                fraction = 0.1d + 0.8d * ((temperature - 70d) / 30d);
            }
            else if (temperature >= 100d)
            {
                // 原式为 0.9 + 0.1 * Math.Min((T - 100.0) / 20.0, 1.0)。
                double over = (temperature - 100d) / 20d;
                if (over > 1d)
                {
                    over = 1d;
                }

                fraction = 0.9d + 0.1d * over;
            }

            int destroyed = (int)((double)extract * fraction);
            if (destroyed > 0)
            {
                FlowReactionMath.SetInt(properties, FlowProps.DreamCapExtract, extract - destroyed);
            }
        }
    }
}
