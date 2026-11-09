using System.Collections.Generic;
using Xunit;

namespace FluxForge.Tests
{
    public class FlowHoldTests
    {
        [Fact]
        public void HoldSeconds_PausesMovement_WithoutBlockedEvent()
        {
            HoldGrid grid = new HoldGrid();
            FlowSpawner flows = new FlowSpawner();
            FlowManager motion = new FlowManager(grid, flows);

            grid.Reactor = new HoldReactor(0.2d);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1d),
                TestFlow.Make(10d, 20d));

            List<bool> blockedChanges = new List<bool>();
            flows.OnEntityBlockedChanged += (current, blocked) =>
                blockedChanges.Add(blocked);

            flows.Accept(entity);

            // 0.5 秒推进到装置格中心，装置设置 0.2 秒主动停顿。
            motion.Tick(0.5d, flows);
            Assert.Equal(1.0d, entity.Position.X);
            Assert.Equal(0.2d, entity.HoldSeconds);

            // 停顿期间不移动，也不产生 Blocked。
            for (int i = 0; i < 3; i++)
            {
                motion.Tick(0.05d, flows);
                Assert.Equal(1.0d, entity.Position.X);
            }

            Assert.Empty(blockedChanges);

            // 最后一次扣完停顿，从装置中心继续移动。
            motion.Tick(0.05d, flows);
            Assert.Equal(0d, entity.HoldSeconds);
            Assert.True(entity.Position.X > 1.0d);
            Assert.Empty(blockedChanges);
        }

        private sealed class HoldReactor : IFlowDevice
        {
            private readonly double _seconds;

            public HoldReactor(double seconds)
            {
                _seconds = seconds;
            }

            public bool TryReact(FlowEntity entity)
            {
                entity.HoldSeconds = _seconds;
                return false;
            }
        }

        private sealed class HoldGrid : IFlowGrid
        {
            public IFlowDevice Reactor { get; set; }

            public bool Contains(int cellX, int cellY)
            {
                return true;
            }

            public FlowCellKind GetCellKind(int cellX, int cellY)
            {
                return FlowCellKind.Empty;
            }

            public IFlowRouter GetRouterAt(int cellX, int cellY)
            {
                return null;
            }

            public IFlowDevice GetReactorAt(int cellX, int cellY)
            {
                return cellX == 1 && cellY == 0 ? Reactor : null;
            }
        }
    }
}
