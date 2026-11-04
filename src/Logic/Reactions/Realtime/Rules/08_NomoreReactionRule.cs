namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleNomoreReaction：梦帽萃取液 + 还原剂 + lipobond → nomore（每 tick 一次）。
    ///
    /// 条件：45–55°C；萃取液、还原剂、lipobond 均 &gt; 0；
    /// 且 alcohol × 10 ≤ 总液量（int 总量），酒精过多整条反应被抑制。
    ///
    /// 每 tick 反应 min(萃取液, 还原剂) 的一部分：基础比例 5%（&lt;50°C）或 10%（≥50°C），
    /// 再乘 lipobond 饱和系数 min(1, lipobond × 4 / 萃取液)。
    /// 但原版有下限：至少反应 min(可反应量, 100 µL)——只加 1 µL lipobond 也按 100 µL 走。
    /// 产物 60% 为 nomore、40% 为 debris；lipobond 与酒精都不消耗。
    /// </summary>
    public sealed class NomoreReactionRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 80; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            if (temperature < 45d || temperature > 55d)
            {
                return;
            }

            int extract = FlowReactionMath.GetInt(properties, FlowProps.DreamCapExtract);
            int reducingAgent = FlowReactionMath.GetInt(properties, FlowProps.ReducingAgent);
            int lipobond = FlowReactionMath.GetInt(properties, FlowProps.LipobondAgent);
            int alcohol = FlowReactionMath.GetInt(properties, FlowProps.Alcohol);
            int nomore = FlowReactionMath.GetInt(properties, FlowProps.Nomore);
            int debris = FlowReactionMath.GetInt(properties, FlowProps.Debris);
            if (extract <= 0 || reducingAgent <= 0 || lipobond <= 0)
            {
                return;
            }

            // 原版用 long 运算和 int 总量比较，酒精量不受溢出影响。
            if ((long)alcohol * 10L > FlowReactionMath.TotalInt(properties))
            {
                return;
            }

            double baseFraction = temperature >= 50d ? 0.1d : 0.05d;
            double saturation = (double)lipobond * 4d / (double)extract;
            if (saturation > 1d)
            {
                saturation = 1d;
            }

            int reactable = FlowReactionMath.Min(extract, reducingAgent);
            int amountToReact = FlowReactionMath.Round((double)reactable * baseFraction * saturation);
            amountToReact = FlowReactionMath.Max(amountToReact, FlowReactionMath.Min(reactable, 100));
            amountToReact = FlowReactionMath.Min(amountToReact, reactable);

            int nomoreProduced = FlowReactionMath.Round((double)amountToReact * 0.6d);
            int debrisProduced = amountToReact - nomoreProduced;

            FlowReactionMath.SetInt(properties, FlowProps.DreamCapExtract, extract - amountToReact);
            FlowReactionMath.SetInt(properties, FlowProps.ReducingAgent, reducingAgent - amountToReact);
            FlowReactionMath.SetInt(properties, FlowProps.Nomore, nomore + nomoreProduced);
            FlowReactionMath.SetInt(properties, FlowProps.Debris, debris + debrisProduced);
        }
    }
}
