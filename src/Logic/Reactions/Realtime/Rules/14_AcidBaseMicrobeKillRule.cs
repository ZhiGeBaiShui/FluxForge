using System;

namespace FluxForge
{
    /// <summary>
    /// 酸碱杀微生物：原版 <c>ChemicalHelper.HandleAcidBaseMicrobeKill</c> 的 1:1 移植。
    ///
    /// 酸 + 碱合计每 1 µL 最多杀灭 20 µL 微生物；试剂消耗为
    /// ceil(杀灭量 / 100)，按「先酸后碱」扣减。
    /// </summary>
    public sealed class AcidBaseMicrobeKillRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 140; }
        }

        public override void React(FlowProperties properties)
        {
            int acidPart = FlowReactionMath.GetInt(properties, FlowProps.Acid);
            int basePart = FlowReactionMath.GetInt(properties, FlowProps.Base);
            int microbePart = FlowReactionMath.GetInt(properties, FlowProps.Microbe);
            if (microbePart <= 0)
            {
                return;
            }

            int totalReagent = acidPart + basePart;
            if (totalReagent <= 0)
            {
                return;
            }

            // 原版以 long 计算杀灭上限，避免 int 溢出。
            long killCap = (long)totalReagent * 20L;
            int microbeToKill = (int)Math.Min((long)microbePart, killCap);
            if (microbeToKill <= 0)
            {
                return;
            }

            int reagentNeeded = (microbeToKill + 99) / 100;
            int acidConsumed = FlowReactionMath.Min(acidPart, reagentNeeded);
            int baseConsumed = FlowReactionMath.Min(basePart, reagentNeeded - acidConsumed);

            FlowReactionMath.SetInt(properties, FlowProps.Microbe, microbePart - microbeToKill);

            if (acidConsumed > 0)
            {
                FlowReactionMath.SetInt(properties, FlowProps.Acid, acidPart - acidConsumed);
            }

            if (baseConsumed > 0)
            {
                FlowReactionMath.SetInt(properties, FlowProps.Base, basePart - baseConsumed);
            }
        }
    }
}
