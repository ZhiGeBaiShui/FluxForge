using System.Collections.Generic;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置的 IFlowState 契约与「按液体种类而非只按水」的行为。
    /// </summary>
    public class FlowStateDeviceTests
    {
        [Fact]
        public void Emitter_NonWaterLiquidStock_EmitsFullPayloadAndClearsLiquidOnly()
        {
            FlowSpawner flows = new FlowSpawner();
            List<FlowEntity> spawned = new List<FlowEntity>();
            flows.OnEntityAdded += spawned.Add;

            EmitterDevice emitter = new EmitterDevice(TestFlow.Make(0d, 20d));
            emitter.Stock.Set(FlowProps.Fat, 50d);
            emitter.Stock.Set(FlowProps.Protein, 25d);
            Assert.True(emitter.HasContent);

            Assert.True(emitter.EmitAll(new FlowPos(0.5d, 0.5d), 1, flows));

            // 载荷完整：纯油库存也能发射，液体与温度都带走
            FlowProperties payload = Assert.Single(spawned).Properties;
            Assert.Equal(50d, payload.Get(FlowProps.Fat));
            Assert.Equal(25d, payload.Get(FlowProps.Protein));
            Assert.Equal(0d, payload.Get(FlowProps.Water));
            Assert.Equal(20d, payload.Get(FlowProps.Temperature));

            // 存量清零全部液体、保留温度
            Assert.Equal(0d, emitter.Stock.SubstanceTotal);
            Assert.Equal(0d, emitter.Stock.Get(FlowProps.Fat));
            Assert.Equal(0d, emitter.Stock.Get(FlowProps.Protein));
            Assert.Equal(0d, emitter.Stock.Get(FlowProps.Water));
            Assert.Equal(20d, emitter.Stock.Get(FlowProps.Temperature));
            Assert.False(emitter.HasContent);

            // 载荷与库存深拷贝独立
            payload.Set(FlowProps.Fat, 1d);
            Assert.Equal(0d, emitter.Stock.Get(FlowProps.Fat));
        }

        [Fact]
        public void Emitter_TemperatureOnly_HasNoContent()
        {
            EmitterDevice emitter = new EmitterDevice(TestFlow.Make(0d, 20d));

            Assert.False(emitter.HasContent);
            Assert.Equal(20d, emitter.Stock.Get(FlowProps.Temperature));
        }

        [Fact]
        public void IceBlock_NonWaterLiquidFlowWithoutWater_DoesNotMelt()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            FlowProperties flow = TestFlow.Make(0d, 25d);
            flow.Set(FlowProps.Fat, 100d);

            Assert.False(ice.TryReact(flow));

            Assert.Equal(10d, ice.Water);
            Assert.Equal(100d, flow.Get(FlowProps.Fat));
            Assert.Equal(25d, flow.Get(FlowProps.Temperature));
        }

        [Fact]
        public void IceBlock_TemperatureWeightUsesTotalLiquidMass()
        {
            // 冰 1000 @-10℃，流源：水 50 + 油 50 @20℃。
            // 融化量按流源水量的一半算 = 25；混合权重用流源总液体质量 100（不是水量 50）。
            IceBlockDevice ice = new IceBlockDevice(1000d, -10d);
            FlowProperties flow = TestFlow.Make(50d, 20d);
            flow.Set(FlowProps.Fat, 50d);

            Assert.True(ice.TryReact(flow));

            Assert.Equal(975d, ice.Water);
            Assert.Equal(75d, flow.Get(FlowProps.Water));
            Assert.Equal(50d, flow.Get(FlowProps.Fat));
            // (20*100 + (-10)*25) / 125 = 14
            Assert.Equal(14d, flow.Get(FlowProps.Temperature), 6);
        }

        [Fact]
        public void Collector_FlowState_RoundTripsEveryRegisteredProperty()
        {
            CollectorDevice collector = new CollectorDevice();

            AssertFlowStateRoundTrip(collector);
        }

        [Fact]
        public void Emitter_FlowState_RoundTripsEveryRegisteredProperty()
        {
            EmitterDevice emitter = new EmitterDevice(TestFlow.Make(0d, 0d));

            AssertFlowStateRoundTrip(emitter);
        }

        [Fact]
        public void IceBlock_FlowState_RoundTripsWaterAndTemperature()
        {
            IceBlockDevice ice = new IceBlockDevice(5d, 6d);
            IFlowState state = ice;

            Assert.Equal(5d, state.Get(FlowProps.Water));
            Assert.Equal(6d, state.Get(FlowProps.Temperature));

            state.Set(FlowProps.Water, 7d);
            state.Set(FlowProps.Temperature, 8d);

            Assert.Equal(7d, ice.Water);
            Assert.Equal(8d, ice.Temperature);
            Assert.Equal(7d, state.Get(FlowProps.Water));
            Assert.Equal(8d, state.Get(FlowProps.Temperature));

            // 其它液体槽位不参与冰块状态：写入被忽略，读回 0，且不影响已有两个槽
            state.Set(FlowProps.Fat, 99d);
            Assert.Equal(0d, state.Get(FlowProps.Fat));
            Assert.Equal(7d, ice.Water);
            Assert.Equal(8d, ice.Temperature);
        }

        /// <summary>每个注册属性（36 液体 + 温度）同时写入不同值，再逐个读回。</summary>
        private static void AssertFlowStateRoundTrip(IFlowState state)
        {
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                state.Set(def.Key, def.Key.Index * 10d);
            }

            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                Assert.Equal(def.Key.Index * 10d, state.Get(def.Key));
            }
        }
    }
}
