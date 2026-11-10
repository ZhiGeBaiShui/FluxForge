using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 过滤公式只处理化学污染物和重金属，不处理微生物/物理污染物。
    /// 温度决定污染占比下限；单次穿过直接达到该下限。
    /// </summary>
    public class FilterReactionTests
    {
        /// <summary>造一份基础水：水 98000 + 化学 1500 + 重金属 500，共 100000，污染占 2%。</summary>
        private static FlowProperties BaseWater()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, 98000d);
            properties.Set(FlowProps.ChemicalContaminant, 1500d);
            properties.Set(FlowProps.HeavyMetal, 500d);
            return properties;
        }

        private static double ContaminantTotal(FlowProperties properties)
        {
            return properties.Get(FlowProps.ChemicalContaminant)
                + properties.Get(FlowProps.HeavyMetal);
        }

        [Fact]
        public void FilterOnlyRemovesChemicalAndHeavyMetal()
        {
            FlowProperties properties = BaseWater();
            properties.Set(FlowProps.Temperature, 100d);
            properties.Set(FlowProps.Microbe, 100d);
            properties.Set(FlowProps.PhysicalContaminant, 50d);

            FilterReaction.React(properties);

            Assert.True(properties.Get(FlowProps.ChemicalContaminant) < 1500d);
            Assert.True(properties.Get(FlowProps.HeavyMetal) < 500d);
            Assert.Equal(100d, properties.Get(FlowProps.Microbe));
            Assert.Equal(50d, properties.Get(FlowProps.PhysicalContaminant));
        }

        [Fact]
        public void FloorDropsOneHundredthPercentPerDegree()
        {
            Assert.Equal(0.0101d, FilterReaction.ContaminantFloorAt(100d), 6);
            Assert.Equal(0.0081d, FilterReaction.ContaminantFloorAt(120d), 6);
            Assert.Equal(0.0010d, FilterReaction.ContaminantFloorAt(191d), 6);
            Assert.Equal(0.0001d, FilterReaction.ContaminantFloorAt(200d), 6);
            Assert.Equal(0d, FilterReaction.ContaminantFloorAt(201d), 6);
            Assert.Equal(0d, FilterReaction.ContaminantFloorAt(300d), 6);
        }

        [Fact]
        public void SinglePassStopsAtTemperatureFloor()
        {
            FlowProperties at100 = BaseWater();
            at100.Set(FlowProps.Temperature, 100d);

            FlowProperties at120 = BaseWater();
            at120.Set(FlowProps.Temperature, 120d);

            FilterReaction.React(at100);
            FilterReaction.React(at120);

            Assert.Equal(
                0.0101d,
                ContaminantTotal(at100) / at100.SubstanceTotal,
                6);
            Assert.Equal(
                0.0081d,
                ContaminantTotal(at120) / at120.SubstanceTotal,
                6);
        }

        [Fact]
        public void TwoHundredDegreesCanReachZeroContamination()
        {
            FlowProperties properties = BaseWater();
            properties.Set(FlowProps.Temperature, 200d);

            FilterReaction.React(properties);

            Assert.Equal(
                0.0001d,
                ContaminantTotal(properties) / properties.SubstanceTotal,
                6);
        }

        [Fact]
        public void TwoHundredOneDegreesRemovesAllContamination()
        {
            FlowProperties properties = BaseWater();
            properties.Set(FlowProps.Temperature, 201d);

            FilterReaction.React(properties);

            Assert.Equal(0d, ContaminantTotal(properties));
        }
    }
}
