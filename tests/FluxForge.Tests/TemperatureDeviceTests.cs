using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 温度控制装置：把自身携带的温度值加到流源上，正升温、负降温，不截停。
    /// </summary>
    public class TemperatureDeviceTests
    {
        private static FlowEntity MakeEntity(double temperature)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, 1000d);
            properties.Set(FlowProps.Temperature, temperature);

            return new FlowEntity(
                new FlowPos(0d, 0d),
                new FlowMotionData(1, 2.5d),
                properties);
        }

        [Fact]
        public void PositiveTemperatureHeatsFlow()
        {
            FlowEntity entity = MakeEntity(20d);
            TemperatureDevice device = new TemperatureDevice(10d);

            bool blocked = device.TryReact(entity);

            Assert.False(blocked);
            Assert.Equal(30d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void NegativeTemperatureCoolsFlow()
        {
            FlowEntity entity = MakeEntity(120d);
            TemperatureDevice device = new TemperatureDevice(-10d);

            device.TryReact(entity);

            Assert.Equal(110d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void ZeroTemperatureLeavesFlowUnchanged()
        {
            FlowEntity entity = MakeEntity(50d);
            TemperatureDevice device = new TemperatureDevice(0d);

            device.TryReact(entity);

            Assert.Equal(50d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void RepeatedPassesAccumulate()
        {
            FlowEntity entity = MakeEntity(20d);
            TemperatureDevice device = new TemperatureDevice(10d);

            device.TryReact(entity);
            device.TryReact(entity);
            device.TryReact(entity);

            Assert.Equal(50d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void OtherPropertiesAreUntouched()
        {
            FlowEntity entity = MakeEntity(20d);
            entity.Properties.Set(FlowProps.ChemicalContaminant, 500d);
            entity.Properties.Set(FlowProps.Water, 2000d);

            TemperatureDevice device = new TemperatureDevice(30d);
            device.TryReact(entity);

            Assert.Equal(500d, entity.Properties.Get(FlowProps.ChemicalContaminant));
            Assert.Equal(2000d, entity.Properties.Get(FlowProps.Water));
        }

        [Fact]
        public void TemperatureIsPublicConfigAndNotConsumedByReaction()
        {
            TemperatureDevice device = new TemperatureDevice(-15d);

            device.TryReact(MakeEntity(10d));

            // 装置温度是 items.json 的静态配置：反应只改流源，不回写装置
            Assert.Equal(-15d, device.Temperature);
        }
    }
}
