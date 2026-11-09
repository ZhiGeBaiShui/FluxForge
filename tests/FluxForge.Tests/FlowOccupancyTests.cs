using System.Collections.Generic;
using Xunit;

namespace FluxForge.Tests
{
    public class FlowOccupancyTests
    {
        private sealed class Grid : IFlowGrid
        {
            public HashSet<(int,int)> Occupied = new();
            public int DeviceCalls;
            public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < 20 && y < 20;
            public FlowCellKind GetCellKind(int x, int y) => Occupied.Contains((x,y))
                ? FlowCellKind.Occupied : FlowCellKind.Empty;
            public IFlowRouter GetRouterAt(int x, int y) { DeviceCalls++; return null; }
            public IFlowDevice GetReactorAt(int x, int y) => null;
        }

        [Theory]
        [InlineData(1, 5, 1, 2, 5)]
        [InlineData(5, 5, 3, 4, 5)]
        [InlineData(5, 1, 2, 5, 2)]
        [InlineData(5, 5, 0, 5, 4)]
        public void FastFlowDiesAtFirstOccupiedCell(int x, int y, int direction, int hitX, int hitY)
        {
            var grid = new Grid(); grid.Occupied.Add((hitX, hitY));
            var flows = new FlowSpawner();
            var flow = new FlowEntity(new FlowPos(x,y), new FlowMotionData(direction,10), TestFlow.Make(100));
            int removed=0; flows.OnEntityRemoved += _ => removed++;
            flows.Accept(flow);
            new FlowManager(grid, flows).Tick(.5, flows);
            Assert.Empty(flows.Active);
            Assert.Equal(1, removed);
            Assert.Equal(0, grid.DeviceCalls);
        }

        [Fact]
        public void PlacingItemOnStationaryFlowKillsItWithoutFurtherReactions()
        {
            var grid = new Grid(); grid.Occupied.Add((2,2));
            var flows = new FlowSpawner();
            var flow = new FlowEntity(new FlowPos(2,2), new FlowMotionData(1,0), TestFlow.Make(100,200));
            flows.Accept(flow); flow.NextReactionTime = -1;
            new FlowManager(grid, flows).Tick(1, flows);
            Assert.Empty(flows.Active);
            Assert.Equal(100, flow.Properties.Get(FlowProps.Water));
        }

        [Fact]
        public void RemovingOccupancyLetsNextFlowPass()
        {
            var grid = new Grid(); grid.Occupied.Add((2,2)); grid.Occupied.Clear();
            var flows = new FlowSpawner();
            flows.Accept(new FlowEntity(new FlowPos(1,2), new FlowMotionData(1,4), TestFlow.Make(100)));
            new FlowManager(grid, flows).Tick(.5, flows);
            Assert.Equal(3, Assert.Single(flows.Active).Position.X);
        }

        [Fact]
        public void StraightDeviceDoesNotStopOrChangeFlow()
        {
            var flows = new FlowSpawner();
            var flow = new FlowEntity(new FlowPos(1,2), new FlowMotionData(1,4), TestFlow.Make(100));
            flows.Accept(flow);
            Assert.False(new StraightDevice().Route(flow, flows, 2, 2));
            Assert.Same(flow, Assert.Single(flows.Active));
            Assert.Equal(100, flow.Properties.SubstanceTotal);
            Assert.Equal(4, flow.Motion.Speed);
        }
    }
}
