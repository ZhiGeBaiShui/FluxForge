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
            TemperatureDevice device = new TemperatureDevice { Temperature = 10d };

            bool blocked = device.TryReact(entity);

            Assert.False(blocked);
            Assert.Equal(30d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void NegativeTemperatureCoolsFlow()
        {
            FlowEntity entity = MakeEntity(120d);
            TemperatureDevice device = new TemperatureDevice { Temperature = -10d };

            device.TryReact(entity);

            Assert.Equal(110d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void ZeroTemperatureLeavesFlowUnchanged()
        {
            FlowEntity entity = MakeEntity(50d);
            TemperatureDevice device = new TemperatureDevice();

            device.TryReact(entity);

            Assert.Equal(50d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void RepeatedPassesAccumulate()
        {
            FlowEntity entity = MakeEntity(20d);
            TemperatureDevice device = new TemperatureDevice { Temperature = 10d };

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

            TemperatureDevice device = new TemperatureDevice { Temperature = 30d };
            device.TryReact(entity);

            Assert.Equal(500d, entity.Properties.Get(FlowProps.ChemicalContaminant));
            Assert.Equal(2000d, entity.Properties.Get(FlowProps.Water));
        }

        [Fact]
        public void FlowStateExposesOnlyTemperature()
        {
            TemperatureDevice device = new TemperatureDevice { Temperature = -15d };

            Assert.Single(device.Keys);
            Assert.Equal(FlowProps.Temperature.Index, device.Keys[0].Index);
            Assert.Equal(-15d, device.Get(FlowProps.Temperature));
            // 其它槽位读 0、写忽略
            Assert.Equal(0d, device.Get(FlowProps.Water));
            device.Set(FlowProps.Water, 999d);
            Assert.Equal(-15d, device.Get(FlowProps.Temperature));
        }

        [Fact]
        public void NullEntityIsIgnored()
        {
            Assert.False(new TemperatureDevice().TryReact(null));
        }
    }
}
