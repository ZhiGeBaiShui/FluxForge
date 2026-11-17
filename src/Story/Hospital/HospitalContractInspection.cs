using System.Collections.Generic;

namespace FluxForge.Story.Hospital
{
    /// <summary>当前选项的桌面检查结果：合格容器、得分、低浓度标记及三种纯试剂体积（微升）。</summary>
    internal sealed class HospitalContractInspection
    {
        internal IReadOnlyList<GameItem> Qualified { get; }
        internal double Score { get; }
        internal bool HasUnderConcentration { get; }
        internal double PureAcidMicrolitres { get; }
        internal double PureBaseMicrolitres { get; }
        internal double PureAlcoholMicrolitres { get; }

        internal HospitalContractInspection(
            IReadOnlyList<GameItem> qualified,
            double score,
            bool hasUnderConcentration,
            double pureAcidMicrolitres,
            double pureBaseMicrolitres,
            double pureAlcoholMicrolitres)
        {
            Qualified = qualified;
            Score = score;
            HasUnderConcentration = hasUnderConcentration;
            PureAcidMicrolitres = pureAcidMicrolitres;
            PureBaseMicrolitres = pureBaseMicrolitres;
            PureAlcoholMicrolitres = pureAlcoholMicrolitres;
        }
    }
}
