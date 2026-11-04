using System;

namespace FluxForge
{
    /// <summary>
    /// 有机废物矿化：原版 <c>ChemicalHelper.HandleOrganicWasteMineralization</c> 的 1:1 移植。
    ///
    /// 酸 + 碱合计每 1 µL 可处理 40 µL 有机废物；实际消耗试剂为
    /// ceil(处理量 / 40)，按「先酸后碱」扣减；矿物产率为 1/10（向下取整）。
    /// </summary>
    public sealed class OrganicWasteMineralizationRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 130; }
        }

        public override void React(FlowProperties properties)
        {
            int acidPart = FlowReactionMath.GetInt(properties, FlowProps.Acid);
            int basePart = FlowReactionMath.GetInt(properties, FlowProps.Base);
            int organicWastePart = FlowReactionMath.GetInt(properties, FlowProps.OrganicWaste);
            int mineralPart = FlowReactionMath.GetInt(properties, FlowProps.Mineral);
            if (organicWastePart <= 0)
            {
                return;
            }

            int totalReagent = acidPart + basePart;
            if (totalReagent <= 0)
            {
                return;
            }

            // 原版以 long 计算处理上限，避免 int 溢出。
            long processCap = (long)totalReagent * 40L;
            int organicWasteToProcess = (int)Math.Min((long)organicWastePart, processCap);
            if (organicWasteToProcess <= 0)
            {
                return;
            }

            int reagentNeeded = (organicWasteToProcess + 39) / 40;
            int mineralProduced = organicWasteToProcess / 10;
            int acidConsumed = FlowReactionMath.Min(acidPart, reagentNeeded);
            int baseConsumed = FlowReactionMath.Min(basePart, reagentNeeded - acidConsumed);

            FlowReactionMath.SetInt(
                properties,
                FlowProps.OrganicWaste,
                organicWastePart - organicWasteToProcess);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Mineral,
                mineralPart + mineralProduced);

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
