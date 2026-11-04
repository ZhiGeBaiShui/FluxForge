namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleCoffeeBeanExtractionWater：咖啡豆的水萃取（每 tick 一次）。
    ///
    /// 温度 &lt;80°C 不反应。每 tick 处理 max(1, round(咖啡豆 × 5%))，不夹到剩余量。
    /// 萃取率的分母是本 tick 处理量：水 ≥5 倍处理量时 12.5%，
    /// 否则按 (水 / 处理量) / 5 线性降低。水不消耗（只作为比例条件）；
    /// 萃取物加进咖啡因，剩余转为植物物质。
    /// </summary>
    public sealed class CoffeeBeanWaterExtractionRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 70; }
        }

        public override void React(FlowProperties properties)
        {
            if (properties.Get(FlowProps.Temperature) < 80d)
            {
                return;
            }

            int coffeeMush = FlowReactionMath.GetInt(properties, FlowProps.CoffeeMush);
            int water = FlowReactionMath.GetInt(properties, FlowProps.Water);
            if (coffeeMush <= 0 || water <= 0)
            {
                return;
            }

            int toProcess = FlowReactionMath.Max(1, FlowReactionMath.Round((double)coffeeMush * 0.05d));

            double ratio = (double)water / (double)toProcess;
            double yield;
            if (ratio < 5d)
            {
                // 原式为 Math.Max(0.0, 0.125 * (ratio / 5.0))；
                // 水量已保证 ratio > 0，这里的夹紧不可达，保留只为对齐原版。
                yield = 0.125d * (ratio / 5d);
                if (yield < 0d)
                {
                    yield = 0d;
                }
            }
            else
            {
                yield = 0.125d;
            }

            int extract = FlowReactionMath.Round((double)toProcess * yield);
            int plantMatter = toProcess - extract;

            FlowReactionMath.SetInt(properties, FlowProps.CoffeeMush, coffeeMush - toProcess);

            int currentExtract = FlowReactionMath.GetInt(properties, FlowProps.Caffeine);
            FlowReactionMath.SetInt(properties, FlowProps.Caffeine, currentExtract + extract);

            int currentPlantMatter = FlowReactionMath.GetInt(properties, FlowProps.PlantMatter);
            FlowReactionMath.SetInt(properties, FlowProps.PlantMatter, currentPlantMatter + plantMatter);
        }
    }
}
