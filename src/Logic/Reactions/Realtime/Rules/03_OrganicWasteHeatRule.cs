namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleOrganicWasteHeat：高温下有机废物分解（每 tick 一次）。
    ///
    /// 温度 &lt;80°C：不写任何槽位；80–100°C：每 tick 处理 15%（向上取整）；
    /// ≥100°C：处理全部。处理量中的 33%（四舍五入）转为物理污染物，其余消失。
    /// </summary>
    public sealed class OrganicWasteHeatRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 30; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            int organicWaste = FlowReactionMath.GetInt(properties, FlowProps.OrganicWaste);
            if (organicWaste <= 0)
            {
                return;
            }

            if (temperature < 80d)
            {
                return;
            }

            int processed;
            if (temperature >= 100d)
            {
                processed = organicWaste;
            }
            else
            {
                processed = FlowReactionMath.Ceiling((double)organicWaste * 0.15d);
            }

            processed = FlowReactionMath.Min(processed, organicWaste);
            int contaminants = FlowReactionMath.Round((double)processed * 0.33d);
            organicWaste -= processed;

            int physicalContaminant = FlowReactionMath.GetInt(properties, FlowProps.PhysicalContaminant);
            physicalContaminant += contaminants;

            FlowReactionMath.SetInt(properties, FlowProps.OrganicWaste, organicWaste);
            FlowReactionMath.SetInt(properties, FlowProps.PhysicalContaminant, physicalContaminant);
        }
    }
}
