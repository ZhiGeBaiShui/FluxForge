using System;

namespace FluxForge
{
    /// <summary>
    /// 脂键剂沉降：原版 <c>ChemicalHelper.HandleLipobondAgentSedimentation</c> 的 1:1 移植。
    ///
    /// 油脂处理上限为 lipobond_agent * 8；实际沉降量为
    /// min(上限, max(1000, 上限 * 20%))，每 8 µL 沉降油脂消耗 1 µL
    /// lipobond_agent（向下取整），沉降油脂与消耗的 lipobond_agent 都计入悬浮颗粒。
    /// </summary>
    public sealed class LipobondAgentSedimentationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 170; }
        }

        public override void React(FlowProperties properties)
        {
            int lipobondAgentPart = FlowReactionMath.GetInt(properties, FlowProps.LipobondAgent);
            int oilPart = FlowReactionMath.GetInt(properties, FlowProps.Fat);
            int debrisPart = FlowReactionMath.GetInt(properties, FlowProps.Debris);
            if (lipobondAgentPart <= 0)
            {
                return;
            }

            if (oilPart <= 0)
            {
                return;
            }

            // 原版以 long 计算油脂上限，避免 int 溢出。
            long oilCap = (long)lipobondAgentPart * 8L;
            int boundedOil = (int)Math.Min((long)oilPart, oilCap);
            int oilToSediment = FlowReactionMath.Min(
                boundedOil,
                FlowReactionMath.Max(1000, (int)((double)boundedOil * 0.2)));
            int lipobondConsumed = oilToSediment / 8;

            FlowReactionMath.SetInt(properties, FlowProps.Fat, oilPart - oilToSediment);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.LipobondAgent,
                lipobondAgentPart - lipobondConsumed);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Debris,
                debrisPart + oilToSediment + lipobondConsumed);
        }
    }
}
