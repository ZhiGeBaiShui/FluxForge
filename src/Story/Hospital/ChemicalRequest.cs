namespace FluxForge.Story.Hospital
{
    /// <summary>合同接受的一种目标化学物。</summary>
    internal sealed class ChemicalRequest
    {
        internal FlowPropertyKey Key { get; }
        internal double MinimumConcentration { get; }
        internal double BaseScore { get; }

        internal ChemicalRequest(
            FlowPropertyKey key,
            double minimumConcentration = 0.90d,
            double baseScore = 1d)
        {
            Key = key;
            MinimumConcentration = minimumConcentration;
            BaseScore = baseScore;
        }
    }
}
