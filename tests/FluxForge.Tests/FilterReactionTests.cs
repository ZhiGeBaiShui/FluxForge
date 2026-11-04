using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 过滤公式只处理化学污染物和重金属，不处理微生物/物理污染物。
    /// 温度决定污染占比下限，速度决定单次去除比例。
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

            FilterReaction.React(properties, 2d);

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
            Assert.Equal(0d, FilterReaction.ContaminantFloorAt(201d), 6);
            Assert.Equal(0d, FilterReaction.ContaminantFloorAt(300d), 6);
        }

        [Fact]
        public void LowerTemperatureStopsAtHigherFloor()
        {
            FlowProperties at100 = BaseWater();
            at100.Set(FlowProps.Temperature, 100d);

            FlowProperties at120 = BaseWater();
            at120.Set(FlowProps.Temperature, 120d);

            // 反复过滤足够多次，让两者都贴到各自下限。
            // 下限是「占比」，而 SubstanceTotal 会随污染物减少而变小，
            // 所以最终收敛到「污染 ÷ 总量 = 下限比例」这个不动点。
            for (int i = 0; i < 200; i++)
            {
                FilterReaction.React(at100, 1d);
                FilterReaction.React(at120, 1d);
            }

            Assert.Equal(0.0101d, ContaminantTotal(at100) / at100.SubstanceTotal, 4);
            Assert.Equal(0.0081d, ContaminantTotal(at120) / at120.SubstanceTotal, 4);
        }

        [Fact]
        public void HigherSpeedReducesRemoval()
        {
            FlowProperties slow = BaseWater();
            slow.Set(FlowProps.Temperature, 100d);

            FlowProperties fast = BaseWater();
            fast.Set(FlowProps.Temperature, 100d);

            FilterReaction.React(slow, 1d);
            FilterReaction.React(fast, 20d);

            double slowRemoved = 2000d - ContaminantTotal(slow);
            double fastRemoved = 2000d - ContaminantTotal(fast);

            Assert.True(slowRemoved > fastRemoved);
        }

        [Fact]
        public void ZeroSpeedRemovesNothing()
        {
            FlowProperties properties = BaseWater();
            properties.Set(FlowProps.Temperature, 200d);

            FilterReaction.React(properties, 0d);

            Assert.Equal(2000d, ContaminantTotal(properties));
        }

        [Fact]
        public void TwoHundredDegreesCanReachZeroContamination()
        {
            FlowProperties properties = BaseWater();
            properties.Set(FlowProps.Temperature, 200d);

            for (int i = 0; i < 500; i++)
            {
                FilterReaction.React(properties, 1d);
            }

            // 200 度下限 0.01%，同样收敛到占比而非固定绝对值。
            Assert.Equal(0.0001d, ContaminantTotal(properties) / properties.SubstanceTotal, 5);
        }
    }
}
