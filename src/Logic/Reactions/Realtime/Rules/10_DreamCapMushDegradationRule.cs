namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleDreamCapMushDegradation：梦帽菇糊的热降解（每 tick 一次）。
    ///
    /// 温度 &lt;50°C 不反应，且菌糊必须 &gt; 0。降解比例按温度分段：
    ///   50–70°C ：0.1 × ((T − 50) / 10)，0 → 0.2（原版分母是 10，跨 20 度）
    ///   70–100°C：0.1 + 0.8 × ((T − 70) / 30)，0.1 → 0.9
    ///   ≥100°C  ：0.9 + 0.1 × min((T − 100) / 20, 1)，0.9 → 1
    /// 降解量用 (int) 截断（不是 Math.Round）；菌糊减少多少，植物物质就增加多少。
    ///
    /// 50–70°C 分支的分母是 10 而不是 20，因此 69.9°C 时比例接近 0.2、
    /// 一到 70°C 立刻掉回 0.1：原版就有的跳变，这里原样保留。
    /// </summary>
    public sealed class DreamCapMushDegradationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 100; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            int mushroom = FlowReactionMath.GetInt(properties, FlowProps.DreamCapMush);
            if (mushroom <= 0)
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
                fraction = 0.1d * ((temperature - 50d) / 10d);
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

            int degraded = (int)((double)mushroom * fraction);
            if (degraded > 0)
            {
                FlowReactionMath.SetInt(properties, FlowProps.DreamCapMush, mushroom - degraded);

                int currentPlantMatter = FlowReactionMath.GetInt(properties, FlowProps.PlantMatter);
                FlowReactionMath.SetInt(properties, FlowProps.PlantMatter, currentPlantMatter + degraded);
            }
        }
    }
}
