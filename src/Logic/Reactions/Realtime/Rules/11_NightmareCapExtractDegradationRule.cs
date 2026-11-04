namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleNightmareCapExtractDegradation：梦魇萃取液的热降解（每 tick 一次）。
    ///
    /// 温度 &lt;50°C 不反应，且萃取液必须 &gt; 0。销毁比例按温度分段：
    ///   50–70°C ：0.1 × ((T − 50) / 2)，0 → 约 1（原版分母是 2，跨 20 度）
    ///   70–100°C：0.1 + 0.8 × ((T − 70) / 30)，0.1 → 0.9
    ///   ≥100°C  ：0.9 + 0.1 × min((T − 100) / 20, 1)，0.9 → 1
    /// 销毁量用 (int) 截断（不是 Math.Round）；只减少萃取液，不产生任何副产物。
    ///
    /// 50–70°C 分支的分母是 2 而不是 20：69°C 就销毁 95%，70°C 回落到 10%。
    /// 这是原版最明显的温度断崖，这里原样保留、不修正。
    /// </summary>
    public sealed class NightmareCapExtractDegradationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 110; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            int extract = FlowReactionMath.GetInt(properties, FlowProps.NightmareExtract);
            if (extract <= 0)
            {
                return;
            }

            if (temperature < 50d)
            {
                return;
            }

            double fraction = 0d;
            if (temperature >= 50d && temperature < 70d)
            {
                fraction = 0.1d * ((temperature - 50d) / 2d);
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
                FlowReactionMath.SetInt(properties, FlowProps.NightmareExtract, extract - destroyed);
            }
        }
    }
}
