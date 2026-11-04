namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleMicrobeAlcohol：酒精浓度足够高时杀死微生物（每 tick 一次）。
    ///
    /// 酒精 ≥ 液体总量的 40% 才生效；每次杀死的量 = min(微生物量, 酒精量)。
    /// 酒精只作为条件，不消耗。
    /// </summary>
    public sealed class MicrobeAlcoholRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 20; }
        }

        public override void React(FlowProperties properties)
        {
            int alcohol = FlowReactionMath.GetInt(properties, FlowProps.Alcohol);
            int microbe = FlowReactionMath.GetInt(properties, FlowProps.Microbe);
            int totalVolume = FlowReactionMath.TotalInt(properties);

            if (alcohol <= 0 || microbe <= 0)
            {
                return;
            }

            if ((double)alcohol < (double)totalVolume * 0.4d)
            {
                return;
            }

            int killed = FlowReactionMath.Min(microbe, alcohol);
            microbe -= killed;

            FlowReactionMath.SetInt(properties, FlowProps.Microbe, microbe);
        }
    }
}
