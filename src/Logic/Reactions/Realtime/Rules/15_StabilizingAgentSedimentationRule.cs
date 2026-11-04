namespace FluxForge
{
    /// <summary>
    /// 稳定剂沉降：原版 <c>ChemicalHelper.HandleStabilizingAgentSedimentation</c> 的 1:1 移植。
    ///
    /// 稳定剂消耗量为 min(酸 + 碱, 稳定剂) 的 20%（下限 1000），并从
    /// 稳定剂槽等量转移到悬浮颗粒；随后酸、碱各自按 5%（下限 100）被移除。
    /// </summary>
    public sealed class StabilizingAgentSedimentationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 150; }
        }

        public override void React(FlowProperties properties)
        {
            int acidPart = FlowReactionMath.GetInt(properties, FlowProps.Acid);
            int basePart = FlowReactionMath.GetInt(properties, FlowProps.Base);
            int stabilizingAgentPart = FlowReactionMath.GetInt(properties, FlowProps.StabilizingAgent);
            int debrisPart = FlowReactionMath.GetInt(properties, FlowProps.Debris);
            if (stabilizingAgentPart <= 0)
            {
                return;
            }

            if (acidPart <= 0 && basePart <= 0)
            {
                return;
            }

            int neutralizable = FlowReactionMath.Min(acidPart + basePart, stabilizingAgentPart);
            int amountToNeutralize = FlowReactionMath.Min(
                neutralizable,
                FlowReactionMath.Max(1000, (int)((double)neutralizable * 0.2)));

            FlowReactionMath.SetInt(
                properties,
                FlowProps.StabilizingAgent,
                stabilizingAgentPart - amountToNeutralize);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Debris,
                debrisPart + amountToNeutralize);

            int acidToRemove = FlowReactionMath.Min(
                acidPart,
                FlowReactionMath.Max(100, (int)((double)acidPart * 0.05)));
            int baseToRemove = FlowReactionMath.Min(
                basePart,
                FlowReactionMath.Max(100, (int)((double)basePart * 0.05)));

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
