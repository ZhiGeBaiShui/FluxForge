using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 反应把流源的液体全部消耗后，运动层应通过 FlowSpawner.Kill 广播移除。
    /// </summary>
    public class FlowManagerReactionCleanupTests
    {
        [Fact]
        public void FlowIsKilledWhenReactionConsumesAllLiquid()
        {
            EmptyGrid grid = new EmptyGrid();
            FlowSpawner flows = new FlowSpawner();
            FlowManager manager = new FlowManager(grid, flows);

            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, 50d);
            properties.Set(FlowProps.Temperature, 101d);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1d),
                properties);

            flows.Accept(entity);
            entity.NextReactionTime = 0.5d;

            manager.Tick(1d, flows);

            Assert.Empty(flows.Active);
        }

        private sealed class EmptyGrid : IFlowGrid
        {
            public bool Contains(int cellX, int cellY)
            {
                return true;
            }

            public IFlowRouter GetRouterAt(int cellX, int cellY)
            {
                return null;
            }

            public IFlowDevice GetReactorAt(int cellX, int cellY)
            {
                return null;
            }
        }
    }
}
