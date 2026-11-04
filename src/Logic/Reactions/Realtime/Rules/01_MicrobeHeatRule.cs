namespace FluxForge
{
    /// <summary>
    /// 原版 ChemicalHelper.HandleMicrobeHeat：温度对微生物的杀灭（每 tick 一次）。
    ///
    /// 温度 ≥100°C：微生物归零；70–100°C：每 tick 保留 80%（int 截断）；
    /// 低于 70°C：数值不变，但原版仍会用相同数值写回槽位，这里保持同样的写回。
    /// </summary>
    public sealed class MicrobeHeatRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 10; }
        }

        public override void React(FlowProperties properties)
        {
            double temperature = properties.Get(FlowProps.Temperature);
            int microbe = FlowReactionMath.GetInt(properties, FlowProps.Microbe);
            if (microbe <= 0)
            {
                return;
            }

            if (temperature >= 100d)
            {
                microbe = 0;
            }
            else if (temperature >= 70d)
            {
                // 原式为 (int)Math.Max(0.0, microbe * 0.8)；此处已保证 microbe > 0，钳位不可能生效。
                microbe = (int)((double)microbe * 0.8d);
            }

            FlowReactionMath.SetInt(properties, FlowProps.Microbe, microbe);
        }
    }
}
