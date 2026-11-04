namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleCoffeeBeanExtractionSolvent：咖啡豆的酒精萃取（每 tick 一次）。
    ///
    /// 温度 &lt;50°C 不反应。每 tick 处理 max(1, round(咖啡豆 × 15%))，不夹到剩余量。
    /// 萃取率的分母是本 tick 处理量：酒精 ≥5 倍处理量时 75%，
    /// 否则按 (酒精 / 处理量) / 5 线性降低。酒精不消耗（只作为比例条件）；
    /// 萃取物加进咖啡因，剩余转为植物物质。
    /// </summary>
    public sealed class CoffeeBeanSolventExtractionRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 60; }
        }

        public override void React(FlowProperties properties)
        {
            if (properties.Get(FlowProps.Temperature) < 50d)
            {
                return;
            }

            int coffeeMush = FlowReactionMath.GetInt(properties, FlowProps.CoffeeMush);
            int alcohol = FlowReactionMath.GetInt(properties, FlowProps.Alcohol);
            if (coffeeMush <= 0 || alcohol <= 0)
            {
                return;
            }

            int toProcess = FlowReactionMath.Max(1, FlowReactionMath.Round((double)coffeeMush * 0.15d));

            double ratio = (double)alcohol / (double)toProcess;
            double yield;
            if (ratio < 5d)
            {
                // 原式为 Math.Max(0.0, 0.75 * (ratio / 5.0))。
                yield = 0.75d * (ratio / 5d);
                if (yield < 0d)
                {
                    yield = 0d;
                }
            }
            else
            {
                yield = 0.75d;
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
