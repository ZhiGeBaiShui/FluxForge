namespace FluxForge
{
    /// <summary>
    /// 甲基醇受热生成芬达：原版 <c>ChemicalHelper.HandleMethylolHeatReaction</c>。
    ///
    /// 前提：甲基醇 > 稳定剂，且还有还原剂可用。
    /// 可用量 = min(甲基醇 - 稳定剂, 还原剂)；
    /// 110–120°C 按可用量的 2.5% 反应，120–130°C 按 15%，
    /// 每轮至少 1000 µL（但不超过可用量）。
    /// 消耗反应量的 75% 还原剂，生成反应量的 10% 芬达。
    /// 区间外直接返回；区间内即使反应量为 0 也按原版写回。
    /// </summary>
    public sealed class MethylolHeatReactionRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 200; }
        }

        public override void React(FlowProperties properties)
        {
            int methylolPart = FlowReactionMath.GetInt(properties, FlowProps.Methylol);
            int reducingAgentPart = FlowReactionMath.GetInt(properties, FlowProps.ReducingAgent);
            int fantaPart = FlowReactionMath.GetInt(properties, FlowProps.Fanta);
            int stabilizingAgentPart = FlowReactionMath.GetInt(properties, FlowProps.StabilizingAgent);

            // 温度是标量，按 double 读取：原版读的就是 GetDouble。
            double temperature = properties.Get(FlowProps.Temperature);

            if (methylolPart <= 0)
            {
                return;
            }

            if (methylolPart <= stabilizingAgentPart)
            {
                return;
            }

            // 稳定剂先抵掉一部分甲基醇，还原剂再封顶一次。
            int reactiveLimit = FlowReactionMath.Min(
                methylolPart - stabilizingAgentPart,
                reducingAgentPart);

            double reactionRatio;
            if (temperature >= 110.0d && temperature < 120.0d)
            {
                reactionRatio = 0.025d;
            }
            else if (temperature >= 120.0d && temperature < 130.0d)
            {
                reactionRatio = 0.15d;
            }
            else
            {
                return;
            }

            // 原版保底 1000，但最终仍被可用量封顶。
            int amountToReact = FlowReactionMath.Min(
                reactiveLimit,
                FlowReactionMath.Max(1000, (int)((double)reactiveLimit * reactionRatio)));

            int reducingAgentAfter = FlowReactionMath.Max(
                0,
                reducingAgentPart - FlowReactionMath.Round((double)amountToReact * 0.75d));

            int fantaAfter = fantaPart + FlowReactionMath.Round((double)amountToReact * 0.1d);

            FlowReactionMath.SetInt(properties, FlowProps.Methylol, methylolPart - amountToReact);
            FlowReactionMath.SetInt(properties, FlowProps.ReducingAgent, reducingAgentAfter);
            FlowReactionMath.SetInt(properties, FlowProps.Fanta, fantaAfter);
        }
    }
}
