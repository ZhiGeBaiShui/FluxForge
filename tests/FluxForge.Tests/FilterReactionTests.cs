using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 过滤公式只处理化学污染物和重金属，不处理微生物/物理污染物。
    /// 温度提高去除上限，速度提高降低单次总容量。
    /// </summary>
    public class FilterReactionTests
    {
        [Fact]
        public void FilterOnlyRemovesChemicalAndHeavyMetal()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 100d);
            properties.Set(FlowProps.ChemicalContaminant, 800d);
            properties.Set(FlowProps.HeavyMetal, 200d);
            properties.Set(FlowProps.Microbe, 100d);
            properties.Set(FlowProps.PhysicalContaminant, 50d);

            FilterReaction.React(properties, 2d);

            Assert.True(properties.Get(FlowProps.ChemicalContaminant) < 800d);
            Assert.True(properties.Get(FlowProps.HeavyMetal) < 200d);
            Assert.Equal(100d, properties.Get(FlowProps.Microbe));
            Assert.Equal(50d, properties.Get(FlowProps.PhysicalContaminant));
        }

        [Fact]
        public void HigherTemperatureAllowsMoreRemoval()
        {
            FlowProperties cold = new FlowProperties();
            cold.Set(FlowProps.Temperature, 0d);
            cold.Set(FlowProps.ChemicalContaminant, 1000d);

            FlowProperties hot = new FlowProperties();
            hot.Set(FlowProps.Temperature, 100d);
            hot.Set(FlowProps.ChemicalContaminant, 1000d);

            FilterReaction.React(cold, 0d);
            FilterReaction.React(hot, 0d);

            double coldRemoved = 1000d - cold.Get(FlowProps.ChemicalContaminant);
            double hotRemoved = 1000d - hot.Get(FlowProps.ChemicalContaminant);

            Assert.True(hotRemoved > coldRemoved);
            Assert.Equal(250d, coldRemoved);
            Assert.Equal(980d, hotRemoved);
        }

        [Fact]
        public void HigherSpeedReducesTotalRemoval()
        {
            FlowProperties slow = new FlowProperties();
            slow.Set(FlowProps.Temperature, 100d);
            slow.Set(FlowProps.ChemicalContaminant, 1000d);

            FlowProperties fast = new FlowProperties();
            fast.Set(FlowProps.Temperature, 100d);
            fast.Set(FlowProps.ChemicalContaminant, 1000d);

            FilterReaction.React(slow, 2d);
            FilterReaction.React(fast, 20d);

            double slowRemoved = 1000d - slow.Get(FlowProps.ChemicalContaminant);
            double fastRemoved = 1000d - fast.Get(FlowProps.ChemicalContaminant);

            Assert.True(slowRemoved > fastRemoved);
        }
    }
}
