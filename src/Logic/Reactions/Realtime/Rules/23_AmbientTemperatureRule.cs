using System;

namespace FluxForge
{
    /// <summary>
    /// 环境降温：完整照搬原版 GetCooledTemp。
    ///
    /// 不是化学反应，但和所有每秒规则共用同一个 tick；Order = 230，
    /// 排在 HandleTemperatureReaction 的 22 条规则之后，保证其它反应先看到
    /// 本 tick 的温度。
    ///
    /// 原版还有 HEATING_TAG 跳过降温；流源没有该 tag，且装置反应是瞬时的，
    /// 因此这里不做实时加热判断。
    /// </summary>
    public sealed class AmbientTemperatureRule : FlowReactionRule
    {
        private const double AmbientTemperature = 26.0d;
        private const double CoolTimeSeconds = 125.0d;
        private const double CoolingFactor = 0.05d;

        public override int Order
        {
            get { return 230; }
        }

        public override void React(FlowProperties properties)
        {
            int volume = FlowReactionMath.TotalInt(properties);
            if (volume <= 0)
            {
                return;
            }

            double initialTemperature = properties.Get(FlowProps.Temperature);
            double cooledTemperature = GetCooledTemp(
                initialTemperature,
                AmbientTemperature,
                volume,
                CoolTimeSeconds,
                CoolingFactor);

            properties.Set(FlowProps.Temperature, cooledTemperature);
        }

        private static double GetCooledTemp(
            double initialTemp,
            double ambientTemp,
            int volume,
            double timeSeconds,
            double coolingFactor)
        {
            if (volume <= 0)
            {
                return initialTemp;
            }

            double rate = coolingFactor / (Math.Sqrt(volume) + 1.0 + 10.0);
            return ambientTemp
                + (initialTemp - ambientTemp)
                * Math.Exp(-rate * timeSeconds);
        }
    }
}
