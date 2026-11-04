using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 过滤装置：穿过时按 <see cref="FilterReaction"/> 净化，不截停流源。
    /// </summary>
    public class FilterDeviceTests
    {
        /// <summary>水 98000 + 指定污染，温度与速度可调。</summary>
        private static FlowEntity MakeEntity(
            double temperature, double chemical, double heavyMetal, double speed)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, 98000d);
            properties.Set(FlowProps.ChemicalContaminant, chemical);
            properties.Set(FlowProps.HeavyMetal, heavyMetal);
            properties.Set(FlowProps.Temperature, temperature);

            return new FlowEntity(
                new FlowPos(0d, 0d),
                new FlowMotionData(1, speed),
                properties);
        }

        private static double ContaminantTotal(FlowEntity entity)
        {
            return entity.Properties.Get(FlowProps.ChemicalContaminant)
                + entity.Properties.Get(FlowProps.HeavyMetal);
        }

        [Fact]
        public void RemovesContaminantsWithoutBlocking()
        {
            FlowEntity entity = MakeEntity(120d, 1500d, 500d, 2.5d);
            FilterDevice device = new FilterDevice();

            bool blocked = device.TryReact(entity);

            // 过滤是穿过型反应，不该截停流源。
            Assert.False(blocked);
            Assert.True(entity.Properties.Get(FlowProps.ChemicalContaminant) < 1500d);
            Assert.True(entity.Properties.Get(FlowProps.HeavyMetal) < 500d);
        }

        [Fact]
        public void LeavesOtherLiquidsUntouched()
        {
            FlowEntity entity = MakeEntity(120d, 1500d, 500d, 2.5d);
            entity.Properties.Set(FlowProps.Microbe, 300d);
            entity.Properties.Set(FlowProps.Sugar, 200d);
            entity.Properties.Set(FlowProps.Debris, 100d);

            new FilterDevice().TryReact(entity);

            Assert.Equal(300d, entity.Properties.Get(FlowProps.Microbe));
            Assert.Equal(200d, entity.Properties.Get(FlowProps.Sugar));
            Assert.Equal(100d, entity.Properties.Get(FlowProps.Debris));
        }

        [Fact]
        public void HigherSpeedRemovesLess()
        {
            FlowEntity slow = MakeEntity(120d, 1500d, 500d, 1.5d);
            FlowEntity fast = MakeEntity(120d, 1500d, 500d, 20d);

            FilterDevice device = new FilterDevice();
            device.TryReact(slow);
            device.TryReact(fast);

            double slowRemoved = 2000d - ContaminantTotal(slow);
            double fastRemoved = 2000d - ContaminantTotal(fast);

            Assert.True(slowRemoved > fastRemoved);
        }

        [Fact]
        public void ColdWaterCannotReachQualityThreshold()
        {
            // 101 度时 floor 是 1.00%，正好压在优质水线上，
            // 单次穿过远达不到，所以污染占比仍在 1% 以上。
            FlowEntity entity = MakeEntity(101d, 1500d, 500d, 2.5d);

            new FilterDevice().TryReact(entity);

            double concentration = ContaminantTotal(entity) / entity.Properties.SubstanceTotal;
            Assert.True(concentration > 0.01d);
        }
    }
}
