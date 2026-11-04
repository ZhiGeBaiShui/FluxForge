namespace FluxForge
{
    /// <summary>
    /// 酸碱中和：原版 <c>ChemicalHelper.HandleAcidBaseNeutralization</c> 的 1:1 移植。
    ///
    /// 酸、碱按较小一方的 10%（下限 1000）等量抵消；
    /// 原版不产生盐类产物，因此只扣减酸 / 碱，不写入任何新成分。
    /// </summary>
    public sealed class AcidBaseNeutralizationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 180; }
        }

        public override void React(FlowProperties properties)
        {
            int acidPart = FlowReactionMath.GetInt(properties, FlowProps.Acid);
            int basePart = FlowReactionMath.GetInt(properties, FlowProps.Base);
            if (acidPart <= 0 || basePart <= 0)
            {
                return;
            }

            int neutralizable = FlowReactionMath.Min(acidPart, basePart);
            int amountToNeutralize = FlowReactionMath.Min(
                neutralizable,
                FlowReactionMath.Max(1000, (int)((double)neutralizable * 0.1)));

            FlowReactionMath.SetInt(properties, FlowProps.Acid, acidPart - amountToNeutralize);
            FlowReactionMath.SetInt(properties, FlowProps.Base, basePart - amountToNeutralize);
        }
    }
}
