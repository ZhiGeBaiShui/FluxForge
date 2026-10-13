using System.Collections.Generic;
using Xunit;
using ProbablyBagMod;

namespace ProbablyBagMod.Tests
{
    public class SimulatorTests
    {
        private static LiquidPayload Liquid(double moisture)
        {
            return new LiquidPayload { Moisture = moisture };
        }

        private static double MoistureOf(FlowToken token)
        {
            return ((LiquidPayload)token.Payload).Moisture;
        }

        private sealed class FakeGrid : IFlowGrid
        {
            private readonly Dictionary<GridPos, IFlowDevice> _devices
                = new Dictionary<GridPos, IFlowDevice>();

            public FakeGrid Set(GridPos pos, IFlowDevice device)
            {
                _devices[pos] = device;
                return this;
            }

            public IFlowDevice GetDeviceAt(GridPos pos)
            {
                return _devices.TryGetValue(pos, out IFlowDevice device) ? device : null;
            }
        }

        [Fact]
        public void Emit_Turn_ReachesOutput()
        {
            var emitter = new EmitterDevice(Direction.Right, () => Liquid(100));

            var grid = new FakeGrid()
                .Set(new GridPos(0, 0), emitter)
                .Set(new GridPos(2, 0), new TurnDevice(Direction.Left, Direction.Down))
                .Set(new GridPos(2, -2), new OutputCollector());

            var sim = new FlowSimulator(grid);
            sim.AddSource(emitter.Spawn(new GridPos(0, 0)));

            sim.Tick();   // (0,0) -> (1,0)
            sim.Tick();   // (1,0) -> (2,0) 转向 Down
            sim.Tick();   // (2,0) -> (2,1)
            sim.Tick();   // (2,1) -> (2,2) 到达输出口

            FlowToken collected = Assert.Single(sim.CollectedOutputs);
            Assert.Equal(100, MoistureOf(collected));
        }

        [Fact]
        public void Split_FeedsTwoOutputs_WithHalfEach()
        {
            var emitter = new EmitterDevice(Direction.Right, () => Liquid(100));

            var grid = new FakeGrid()
                .Set(new GridPos(0, 0), emitter)
                .Set(new GridPos(2, 0), new SplitDevice(Direction.Left, Direction.Down, Direction.Up))
                .Set(new GridPos(2, 2), new OutputCollector())
                .Set(new GridPos(2, -2), new OutputCollector());

            var sim = new FlowSimulator(grid);
            sim.AddSource(emitter.Spawn(new GridPos(0, 0)));

            for (int i = 0; i < 12; i++)
            {
                sim.Tick();
            }

            Assert.Equal(2, sim.CollectedOutputs.Count);
            foreach (FlowToken token in sim.CollectedOutputs)
            {
                Assert.Equal(50, MoistureOf(token));
            }
        }

        [Fact]
        public void TwoSources_Merge_ThenReachOutput()
        {
            var sourceA = new EmitterDevice(Direction.Right, () => Liquid(40));
            var sourceB = new EmitterDevice(Direction.Up, () => Liquid(60));
            var posA = new GridPos(-2, 0);
            var posB = new GridPos(0, -2);

            var grid = new FakeGrid()
                .Set(posA, sourceA)
                .Set(posB, sourceB)
                .Set(new GridPos(0, 0), new MergeDevice(2, Direction.Right))
                .Set(new GridPos(2, 0), new OutputCollector());

            var sim = new FlowSimulator(grid);
            sim.AddSource(sourceA.Spawn(posA));
            sim.AddSource(sourceB.Spawn(posB));

            for (int i = 0; i < 10; i++)
            {
                sim.Tick();
            }

            FlowToken collected = Assert.Single(sim.CollectedOutputs);
            Assert.Equal(100, MoistureOf(collected));   // 40 + 60，总量守恒
        }

        [Fact]
        public void CancelAll_StopsEverything()
        {
            var emitter = new EmitterDevice(Direction.Right, () => Liquid(100));
            var position = new GridPos(0, 0);

            var grid = new FakeGrid().Set(position, emitter);

            var sim = new FlowSimulator(grid);
            sim.AddSource(emitter.Spawn(position));

            sim.CancelAll();

            for (int i = 0; i < 20; i++)
            {
                sim.Tick();
            }

            Assert.Empty(sim.CollectedOutputs);
        }

        [Fact]
        public void FlowEnterDevice_EventFires()
        {
            var emitter = new EmitterDevice(Direction.Right, () => Liquid(100));
            var position = new GridPos(0, 0);

            var grid = new FakeGrid()
                .Set(position, emitter)
                .Set(new GridPos(2, 0), new TurnDevice(Direction.Left, Direction.Down));

            var sim = new FlowSimulator(grid);
            sim.AddSource(emitter.Spawn(position));

            int fired = 0;
            sim.OnFlowEnterDevice += (token, device) => fired++;

            for (int i = 0; i < 3; i++)
            {
                sim.Tick();
            }

            Assert.True(fired > 0, "进入装置时应触发 OnFlowEnterDevice");
        }
    }
}
