namespace FluxForge
{
    /// <summary>
    /// 蛋白质沉淀：原版 <c>ChemicalHelper.HandleProteinPrecipitation</c> 的 1:1 移植。
    ///
    /// 只有酸 / 碱不平衡（|酸 - 碱|）才触发：最多变性
    /// min(不平衡量, 蛋白质) 的 20%（至少 1 µL），变性蛋白质转入悬浮颗粒，
    /// 并从过量的一侧等量扣除酸或碱。
    /// </summary>
    public sealed class ProteinPrecipitationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 160; }
        }

        public override void React(FlowProperties properties)
        {
            int acidPart = FlowReactionMath.GetInt(properties, FlowProps.Acid);
            int basePart = FlowReactionMath.GetInt(properties, FlowProps.Base);
            int proteinPart = FlowReactionMath.GetInt(properties, FlowProps.Protein);
            int debrisPart = FlowReactionMath.GetInt(properties, FlowProps.Debris);
            if (proteinPart <= 0)
            {
                return;
            }

            int acidExcess = FlowReactionMath.Max(0, acidPart - basePart);
            int baseExcess = FlowReactionMath.Max(0, basePart - acidPart);
            int totalImbalance = acidExcess + baseExcess;
            if (totalImbalance <= 0)
            {
                return;
            }

            int precipitable = FlowReactionMath.Min(totalImbalance, proteinPart);
            int amountToDenature = FlowReactionMath.Max(1, (int)((double)precipitable * 0.2));
            amountToDenature = FlowReactionMath.Min(amountToDenature, precipitable);
            int acidToRemove = FlowReactionMath.Min(acidExcess, amountToDenature);
            int baseToRemove = FlowReactionMath.Min(baseExcess, amountToDenature - acidToRemove);

            FlowReactionMath.SetInt(
                properties,
                FlowProps.Protein,
                proteinPart - amountToDenature);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Debris,
                debrisPart + amountToDenature);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Acid,
                FlowReactionMath.Max(0, acidPart - acidToRemove));
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Base,
                FlowReactionMath.Max(0, basePart - baseToRemove));
        }
    }
}
