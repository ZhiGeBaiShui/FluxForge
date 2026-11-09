using System.Collections.Generic;
using Xunit;
namespace FluxForge.Tests
{
    public class FlowInletTests
    {
        private sealed class Reactor : IFlowDevice
        {
            public int Calls;
            public bool TryReact(FlowEntity entity) { Calls++; return false; }
        }
        private sealed class Grid : IFlowGrid
        {
            public IFlowRouter Router;
            public Reactor Reactor = new Reactor();
            public bool Contains(int x,int y) => x >= 0 && y >= 0 && x < 20 && y < 20;
            public FlowCellKind GetCellKind(int x,int y) => x==5 && y==5 ? FlowCellKind.Device : FlowCellKind.Empty;
            public IFlowRouter GetRouterAt(int x,int y) => x==5 && y==5 ? Router : null;
            public IFlowDevice GetReactorAt(int x,int y) => x==5 && y==5 ? Reactor : null;
        }
        public static IEnumerable<object[]> Directions
        {
            get {
                for(int facing=0;facing<4;facing++)
                for(int direction=0;direction<4;direction++)
                foreach(bool flipped in new[]{false,true})
                    yield return new object[]{facing,direction,flipped};
            }
        }
        [Theory]
        [MemberData(nameof(Directions))]
        public void StraightOnlyAcceptsBothAxialDirections(int facing,int direction,bool flipped)
        {
            var pipe=new StraightDevice { Facing=facing, Flipped=flipped };
            var grid=new Grid { Router=pipe };
            var flows=new FlowSpawner();
            var flow=new FlowEntity(new FlowPos(5-2*FlowMotionData.StepX(direction),5-2*FlowMotionData.StepY(direction)),
                new FlowMotionData(direction,10),TestFlow.Make(100));
            flows.Accept(flow);
            new FlowManager(grid,flows).Tick(.3,flows);
            bool allowed=direction%2==facing%2;
            Assert.Equal(allowed, flows.Active.Count==1);
            Assert.Equal(allowed ? 1:0,grid.Reactor.Calls);
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void CollectorOnlyAcceptsItsIntake(int facing)
        {
            var collector=new CollectorDevice(TestFlow.Make(0)) { Facing=facing };
            for(int direction=0;direction<4;direction++)
                Assert.Equal(direction==(facing+2)%4,collector.CanEnter(direction));
        }
        [Theory]
        [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
        public void SpeedControllerUsesSameAxisRestriction(int facing)
        {
            var pipe=new StraightSpeedDevice(1,10,.1) { Facing=facing };
            for(int direction=0;direction<4;direction++)
                Assert.Equal(direction%2==facing%2,pipe.CanEnter(direction));
        }
        [Theory]
        [MemberData(nameof(Directions))]
        public void TurnUsesRotatedAndMirroredOpenings(int facing,int direction,bool flipped)
        {
            var turn=new TurnDevice { Facing=facing,Flipped=flipped };
            int side=(direction+2)%4;
            int horizontal=((flipped?1:3)+facing)%4;
            int vertical=(2+facing)%4;
            bool allowed=side==horizontal || side==vertical;
            Assert.Equal(allowed,turn.CanEnter(direction));
            var grid=new Grid { Router=turn };
            var flows=new FlowSpawner();
            flows.Accept(new FlowEntity(new FlowPos(5-2*FlowMotionData.StepX(direction),5-2*FlowMotionData.StepY(direction)),
                new FlowMotionData(direction,10),TestFlow.Make(100)));
            var manager=new FlowManager(grid,flows);
            manager.Tick(.2,flows);
            manager.Tick(.2,flows);
            Assert.Equal(allowed,flows.Active.Count==1);
            Assert.Equal(allowed?1:0,grid.Reactor.Calls);
        }
    }
}
