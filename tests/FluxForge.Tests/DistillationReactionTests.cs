using Xunit;

namespace FluxForge.Tests
{
    public class DistillationReactionTests
    {
        private static FlowProperties Mixture(double water, double other)
        {
            var properties = TestFlow.Make(water, 80);
            properties.Set(FlowProps.HeavyMetal, other);
            return properties;
        }

        [Theory]
        [InlineData(80, 20, 100, 60, 20)]
        [InlineData(80, 20, 10, 10, 70)]
        [InlineData(50, 50, 100, 0, 50)]
        [InlineData(40, 60, 100, 0, 40)]
        [InlineData(80, 20, 0, 0, 80)]
        [InlineData(0, 0, 100, 0, 0)]
        [InlineData(100, 0, 100, 0, 100)]
        public void RemovesOnlyWaterWithinBudgetAndPurityLimit(
            double water, double other, double budget, double expected, double remaining)
        {
            var properties = Mixture(water, other);
            double extracted = DistillationReaction.React(properties, budget);
            Assert.Equal(expected, extracted);
            Assert.Equal(remaining, properties.Get(FlowProps.Water));
            Assert.Equal(other, properties.Get(FlowProps.HeavyMetal));
            Assert.Equal(80, properties.Get(FlowProps.Temperature));
            Assert.Equal(water + other, properties.SubstanceTotal + extracted);
        }

        [Fact]
        public void RepeatedPassCannotExtractBeyondHalfWater()
        {
            var properties = Mixture(80, 20);
            Assert.Equal(60, DistillationReaction.React(properties, 100));
            Assert.Equal(0, DistillationReaction.React(properties, 100));
            Assert.Equal(.5, properties.Get(FlowProps.Water) / properties.SubstanceTotal);
        }
    }
}
