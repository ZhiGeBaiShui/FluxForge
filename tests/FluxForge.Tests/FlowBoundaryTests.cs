using Xunit;
namespace FluxForge.Tests
{
    public class FlowBoundaryTests
    {
        private sealed class Device : IFlowDevice, IFlowInlet
        {
            public bool Allowed;
            public int EntryCalls, Reactions;
            public bool CanEnter(int direction) { EntryCalls++; return Allowed; }
            public bool TryReact(FlowEntity entity) { Reactions++; return false; }
        }
        private sealed class Grid : IFlowGrid
        {
            public Device Device = new Device();
            public bool Solid;
            public bool Contains(int x,int y) => x>=0 && y>=0 && x<20 && y<20;
            public FlowCellKind GetCellKind(int x,int y) => x==5 && y==5
                ? (Solid?FlowCellKind.Occupied:FlowCellKind.Device) : FlowCellKind.Empty;
            public IFlowRouter GetRouterAt(int x,int y) => null;
            public IFlowDevice GetReactorAt(int x,int y) => x==5 && y==5 ? Device:null;
        }
        private static FlowEntity Flow(int direction,double distance)
        {
            return new FlowEntity(new FlowPos(5-FlowMotionData.StepX(direction)*distance,
                5-FlowMotionData.StepY(direction)*distance),new FlowMotionData(direction,1),TestFlow.Make(100));
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void InvalidEntryDiesAtEdgeBeforeReachingCenter(int direction)
        {
            var grid=new Grid(); var flows=new FlowSpawner(); var entity=Flow(direction,1);
            flows.Accept(entity); var motion=new FlowManager(grid,flows);
            motion.Tick(.49,flows);
            Assert.Single(flows.Active); Assert.Equal(0,grid.Device.EntryCalls);
            motion.Tick(.02,flows);
            Assert.Empty(flows.Active); Assert.Equal(1,grid.Device.EntryCalls);
            Assert.Equal(0,grid.Device.Reactions);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void AllowedEntryReactsAtOneThirdWithoutRecheckingEntry(int direction)
        {
            var grid=new Grid(); grid.Device.Allowed=true;
            var flows=new FlowSpawner(); flows.Accept(Flow(direction,1)); var motion=new FlowManager(grid,flows);
            motion.Tick(.5,flows);
            Assert.Equal(1,grid.Device.EntryCalls); Assert.Equal(0,grid.Device.Reactions);
            motion.Tick(.25,flows);
            Assert.Equal(0,grid.Device.Reactions);
            motion.Tick(.09,flows);
            Assert.Equal(1,grid.Device.Reactions); Assert.Equal(1,grid.Device.EntryCalls);
            motion.Tick(.4,flows);
            Assert.Equal(1,grid.Device.Reactions);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void BornAtDeviceCenterCanLeaveWithoutEntryCheck(int direction)
        {
            var grid=new Grid(); var flows=new FlowSpawner(); flows.Accept(Flow(direction,0));
            new FlowManager(grid,flows).Tick(1,flows);
            Assert.Single(flows.Active); Assert.Equal(0,grid.Device.EntryCalls);
            Assert.Equal(0,grid.Device.Reactions);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void OrdinaryItemKillsAtBoundaryToo(int direction)
        {
            var grid=new Grid { Solid=true }; var flows=new FlowSpawner(); flows.Accept(Flow(direction,1));
            new FlowManager(grid,flows).Tick(.5,flows);
            Assert.Empty(flows.Active); Assert.Equal(0,grid.Device.EntryCalls);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void HighSpeedCrossingCannotSkipEntry(int direction)
        {
            var grid=new Grid(); var flows=new FlowSpawner(); var entity=Flow(direction,3);
            entity.Motion.Speed=10; flows.Accept(entity);
            new FlowManager(grid,flows).Tick(.6,flows);
            Assert.Empty(flows.Active); Assert.Equal(0,grid.Device.Reactions);
        }
    }
}
