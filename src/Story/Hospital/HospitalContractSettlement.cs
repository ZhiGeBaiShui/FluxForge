namespace FluxForge.Story.Hospital
{
    /// <summary>一次医院取货的结算结果。</summary>
    internal sealed class HospitalContractSettlement
    {
        internal double Score { get; }
        internal int ReputationDelta { get; }
        internal int Payment { get; }
        internal int ItemCount { get; }
        internal bool HasHighPurity { get; }

        internal HospitalContractSettlement(
            double score,
            int reputationDelta,
            int payment,
            int itemCount,
            bool hasHighPurity)
        {
            Score = score;
            ReputationDelta = reputationDelta;
            Payment = payment;
            ItemCount = itemCount;
            HasHighPurity = hasHighPurity;
        }
    }
}
