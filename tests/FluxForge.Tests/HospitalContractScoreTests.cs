using FluxForge.Story.Hospital;
using Xunit;

namespace FluxForge.Tests
{
    public class HospitalContractScoreTests
    {
        [Theory]
        [InlineData(0.90d, 1.0d)]
        [InlineData(0.989d, 1.0d)]
        [InlineData(0.99d, 1.0d)]
        [InlineData(0.995d, 1.5d)]
        [InlineData(1.0d, 3.0d)]
        public void WeightMatchesConcentrationTiers(
            double concentration,
            double expected)
        {
            Assert.Equal(
                expected,
                HospitalContractScore.Weight(concentration),
                6);
        }

        [Theory]
        [InlineData(300d, 3)]
        [InlineData(299d, -1)]
        [InlineData(0d, -3)]
        [InlineData(900d, 9)]
        public void ReputationDeltaUsesTargetAndCap(
            double score,
            int expected)
        {
            Assert.Equal(
                expected,
                HospitalContractScore.ReputationDelta(score));
        }

        [Fact]
        public void PositiveReputationIsCappedAtTwenty()
        {
            Assert.Equal(
                20,
                HospitalContractScore.ReputationDelta(100000d));
        }

        [Fact]
        public void BaseScoreMultipliesPerMillilitre()
        {
            Assert.Equal(
                150d,
                HospitalContractScore.Score(100d, 1d, 0.995d),
                6);
            Assert.Equal(
                240d,
                HospitalContractScore.Score(100d, 0.8d, 1d),
                6);
        }

        [Fact]
        public void PaymentUsesHalfCreditPerPoint()
        {
            Assert.Equal(150, HospitalContractScore.Payment(300d));
        }
    }
}
