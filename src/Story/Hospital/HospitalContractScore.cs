using System;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院合同的纯计分规则，不依赖 Unity 或存档。</summary>
    internal static class HospitalContractScore
    {
        internal const double MinimumConcentration = 0.90d;
        internal const double PurityThreshold = 0.99d;
        internal const int Target = 300;
        internal const int MaxPositivePerContract = 20;
        internal const double PaymentRate = 0.1d;
        internal const int TrustReputationThreshold = 60;
        internal const int ExternalExpertReputationThreshold = 100;
        private const double TrustPaymentMultiplier = 1.2d;

        private const double Epsilon = 1e-9d;

        internal static double Weight(double concentration)
        {
            if (concentration + Epsilon < PurityThreshold)
            {
                return 1d;
            }

            if (concentration + Epsilon >= 1d)
            {
                return 3d;
            }

            return 1d + (concentration - PurityThreshold) * 100d;
        }

        internal static int ReputationDelta(double score)
        {
            if (score >= Target)
            {
                return Math.Min(
                    (int)Math.Floor(score / 100d),
                    MaxPositivePerContract);
            }

            return -(int)Math.Ceiling((Target - score) / 100d);
        }

        internal static int ReputationDelta(double score, int reputation)
        {
            return reputation >= ExternalExpertReputationThreshold && score < Target
                ? 0 : ReputationDelta(score);
        }

        internal static double Score(
            double millilitres,
            double baseScore,
            double concentration)
        {
            return millilitres * baseScore * Weight(concentration);
        }

        internal static int Payment(double score)
        {
            return (int)Math.Floor(score * PaymentRate);
        }

        internal static int Payment(double score, int reputation)
        {
            int payment = Payment(score);
            return reputation >= TrustReputationThreshold
                ? (int)Math.Floor(payment * TrustPaymentMultiplier)
                : payment;
        }
    }
}
