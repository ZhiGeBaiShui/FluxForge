namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleNightmareCapMushReaction：夜魇菇的酒精萃取（每 tick 一次）。
    ///
    /// 与梦帽菇规则同构：温度 &lt;40°C 不反应；每 tick 处理 max(2000, round(菌糊 × 10%))，
    /// 再夹到不超过菌糊。萃取率的分母是菌糊总量（不是本 tick 处理量）：
    /// 酒精 ≥5 倍菌糊时 50%，否则按 酒精 / 菌糊 / 5 线性降低。
    /// 酒精不消耗；萃取物加进夜魇萃取液，剩余菌糊转为植物物质。
    /// </summary>
    public sealed class NightmareCapMushReactionRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 50; }
        }

        public override void React(FlowProperties properties)
        {
            if (properties.Get(FlowProps.Temperature) < 40d)
            {
                return;
            }

            int mushroom = FlowReactionMath.GetInt(properties, FlowProps.NightmareCapMush);
            int alcohol = FlowReactionMath.GetInt(properties, FlowProps.Alcohol);
            if (mushroom <= 0 || alcohol <= 0)
            {
                return;
            }

            int toProcess = FlowReactionMath.Max(2000, FlowReactionMath.Round((double)mushroom * 0.1d));
            toProcess = FlowReactionMath.Min(toProcess, mushroom);

            double ratio = (double)alcohol / (double)mushroom;
            double yield;
            if (ratio >= 5d)
            {
                yield = 0.5d;
            }
            else
            {
                // 原式为 Math.Max(0.0, 0.5 * (ratio / 5.0))。
                yield = 0.5d * (ratio / 5d);
                if (yield < 0d)
                {
                    yield = 0d;
                }
            }

            int extract = FlowReactionMath.Round((double)toProcess * yield);
            int plantMatter = toProcess - extract;

            FlowReactionMath.SetInt(properties, FlowProps.NightmareCapMush, mushroom - toProcess);

            int currentExtract = FlowReactionMath.GetInt(properties, FlowProps.NightmareExtract);
            FlowReactionMath.SetInt(properties, FlowProps.NightmareExtract, currentExtract + extract);

            int currentPlantMatter = FlowReactionMath.GetInt(properties, FlowProps.PlantMatter);
            FlowReactionMath.SetInt(properties, FlowProps.PlantMatter, currentPlantMatter + plantMatter);
        }
    }
}
