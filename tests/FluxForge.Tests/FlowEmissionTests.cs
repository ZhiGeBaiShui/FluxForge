using Xunit;
namespace FluxForge.Tests
{
    public class FlowEmissionTests
    {
        private sealed class Grid : IFlowGrid
        {
            public FlowCellKind GetCellKind(int x, int y) => FlowCellKind.Empty;
            public bool Contains(int x, int y) => x >= 0 && y >= 0 && x < 3 && y < 3;
            public IFlowRouter GetRouterAt(int x, int y) => null;
            public IFlowDevice GetReactorAt(int x, int y) => null;
        }

        [Fact]
        public void EmitsIndependentCopyAndBroadcastsOnce()
        {
            var flows = new FlowSpawner();
            var input = TestFlow.Make(100, 25);
            var origin = new FlowPos(1, 2);
            int added = 0;
            flows.OnEntityAdded += e => added++;
            Assert.True(flows.TryEmit(input, origin, 1, 5, new Grid()));
            var emitted = Assert.Single(flows.Active);
            input.Set(FlowProps.Water, 0);
            origin.Set(0, 0);
            Assert.Equal(100, emitted.Properties.Get(FlowProps.Water));
            Assert.Equal(25, emitted.Properties.Get(FlowProps.Temperature));
            Assert.Equal(1, emitted.Position.X);
            Assert.Equal(2, emitted.Position.Y);
            Assert.Equal(1, emitted.Motion.Direction);
            Assert.Equal(5, emitted.Motion.Speed);
            Assert.Equal(1, added);
        }

        [Theory]
        [InlineData(-0.01, 0, 0, 1)]
        [InlineData(3, 0, 0, 1)]
        public void OutsideGridKeepsPayloadAndCreatesNothing(double x, double y, int direction, double speed)
        {
            var flows = new FlowSpawner();
            var input = TestFlow.Make(100);
            Assert.False(flows.TryEmit(input, new FlowPos(x, y), direction, speed, new Grid()));
            Assert.Empty(flows.Active);
            Assert.Equal(100, input.SubstanceTotal);
        }

        [Fact]
        public void ForeignOnlyPayloadCanEmitAndKeepsVolume()
        {
            var flows = new FlowSpawner();
            var input = new FlowProperties();
            input.Set(FlowProps.Foreign, 400);

            Assert.True(flows.TryEmit(
                input,
                new FlowPos(1, 2),
                1,
                5,
                new Grid()));

            var emitted = Assert.Single(flows.Active);
            Assert.Equal(400, emitted.Properties.Get(FlowProps.Foreign));
            Assert.Equal(400, emitted.Properties.SubstanceTotal);
        }

        [Fact]
        public void EmptyLiquidCannotEmit()
        {
            var flows = new FlowSpawner();
            Assert.False(flows.TryEmit(TestFlow.Make(0), new FlowPos(0, 0), 0, 1, new Grid()));
            Assert.Empty(flows.Active);
        }
    }
}
