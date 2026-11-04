using System;

namespace FluxForge
{
    /// <summary>
    /// 氯消毒：原版 <c>ChemicalHelper.HandleChlorine</c>。
    ///
    /// 每轮最多投入 15000 µL 氯；1 µL 氯可清除 5.1 µL 化学污染物或微生物。
    /// 化学污染物先扣掉总量 0.7% 的容忍量，剩余部分才被氯消耗；
    /// 消化完污染物还有余力才轮到微生物。
    /// 投入的氯用完后，剩余氯自降解 20%（至少 1000，且不超过其剩余量）。
    /// 须同时有氯和水，否则直接返回。
    /// </summary>
    public sealed class ChlorineRule : FlowReactionRule
    {
        public override int Order
        {
            get { return 210; }
        }

        public override void React(FlowProperties properties)
        {
            int chlorinePart = FlowReactionMath.GetInt(properties, FlowProps.Chlorine);
            int microbePart = FlowReactionMath.GetInt(properties, FlowProps.Microbe);
            int chemicalPart = FlowReactionMath.GetInt(properties, FlowProps.ChemicalContaminant);
            int waterPart = FlowReactionMath.GetInt(properties, FlowProps.Water);
            int totalVolume = FlowReactionMath.TotalInt(properties);
            if (chlorinePart <= 0 || waterPart <= 0)
            {
                return;
            }

            int chlorineToReact = FlowReactionMath.Min(chlorinePart, 15000);

            // 原版用 long 承载「5.1 倍」的清除容量，避免大容量溢出。
            long reactionCapacity = (long)((double)chlorineToReact * 5.1d);

            // 原版此处是 float 运算：0.7% 容忍量，精度与结果按 float 还原。
            int contaminantTolerance = (int)((float)totalVolume * 0.007f);

            int chemicalsDestroyed = 0;
            if (chemicalPart > contaminantTolerance)
            {
                // 原版 Math.Min(int, long) 走 long 重载，再转回 int。
                chemicalsDestroyed = (int)Math.Min(
                    (long)(chemicalPart - contaminantTolerance),
                    reactionCapacity);
            }

            reactionCapacity -= chemicalsDestroyed;
            int newChemicals = chemicalPart - chemicalsDestroyed;

            int microbesDestroyed = (int)Math.Min((long)microbePart, reactionCapacity);

            // 投入部分被消耗，剩余部分再自降解。
            int chlorineAfterReaction = chlorinePart - chlorineToReact;
            int selfDegraded = FlowReactionMath.Min(
                chlorineAfterReaction,
                FlowReactionMath.Max(1000, (int)((double)chlorineAfterReaction * 0.2d)));

            FlowReactionMath.SetInt(
                properties,
                FlowProps.Chlorine,
                chlorineAfterReaction - selfDegraded);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.Microbe,
                microbePart - microbesDestroyed);
            FlowReactionMath.SetInt(
                properties,
                FlowProps.ChemicalContaminant,
                newChemicals);
        }
    }
}
