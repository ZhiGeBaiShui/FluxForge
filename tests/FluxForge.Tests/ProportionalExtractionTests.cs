using FluxForge;
using Xunit;

namespace FluxForge.Tests
{
    public class ProportionalExtractionTests
    {
        [Fact]
        public void Nightmare_AtHalfSaturation_ConvertsQuarter()
        {
            FlowProperties flow = TestFlow.Make(0d, 40d);
            flow.Set(FlowProps.NightmareCapMush, 1000d);
            flow.Set(FlowProps.Alcohol, 2000d);

            Assert.True(NightmareCapExtractionReaction.React(flow));

            Assert.Equal(0d, flow.Get(FlowProps.NightmareCapMush), 6);
            Assert.Equal(250d, flow.Get(FlowProps.NightmareExtract), 6);
            Assert.Equal(750d, flow.Get(FlowProps.PlantMatter), 6);
            Assert.Equal(2000d, flow.Get(FlowProps.Alcohol), 6);
        }

        [Fact]
        public void Dream_AtSaturation_ConvertsHalf()
        {
            FlowProperties flow = TestFlow.Make(0d, 40d);
            flow.Set(FlowProps.DreamCapMush, 1000d);
            flow.Set(FlowProps.Alcohol, 4000d);

            Assert.True(DreamCapExtractionReaction.React(flow));

            Assert.Equal(0d, flow.Get(FlowProps.DreamCapMush), 6);
            Assert.Equal(500d, flow.Get(FlowProps.DreamCapExtract), 6);
            Assert.Equal(500d, flow.Get(FlowProps.PlantMatter), 6);
        }

        [Fact]
        public void CoffeeAlcohol_AtSaturation_ConvertsSeventyFivePercent()
        {
            FlowProperties flow = TestFlow.Make(0d, 50d);
            flow.Set(FlowProps.CoffeeMush, 1000d);
            flow.Set(FlowProps.Alcohol, 4000d);

            Assert.True(CoffeeBeanSolventExtractionReaction.React(flow));

            Assert.Equal(0d, flow.Get(FlowProps.CoffeeMush), 6);
            Assert.Equal(750d, flow.Get(FlowProps.Caffeine), 6);
            Assert.Equal(250d, flow.Get(FlowProps.PlantMatter), 6);
        }

        [Fact]
        public void CoffeeAlcohol_RequiresZeroPointSevenFiveRatio()
        {
            FlowProperties flow = TestFlow.Make(0d, 50d);
            flow.Set(FlowProps.CoffeeMush, 1000d);
            flow.Set(FlowProps.Alcohol, 750d);

            Assert.True(CoffeeBeanSolventExtractionReaction.React(flow));

            Assert.Equal(750d, flow.Get(FlowProps.Caffeine), 6);
            Assert.Equal(250d, flow.Get(FlowProps.PlantMatter), 6);
        }
    }
}
