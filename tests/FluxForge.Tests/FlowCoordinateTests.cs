using System;
using System.Collections.Generic;
using FluxForge.Presentation.Flow.Particles;
using FluxForge.Presentation.Flow.Visual;
using Xunit;

namespace FluxForge.Tests
{
    public class FlowCoordinateTests
    {
        [Theory]
        [InlineData(0.5d, 0d)]
        [InlineData(16.5d, 16d)]
        [InlineData(8.990262810140848d, 8.490262810140848d)]
        [InlineData(-0.25d, -0.75d)]
        public void TileLeft_IsHalfCellBeforeGameCenter(double gameCenter, double tileLeft)
        {
            double actual = FlowPixelMath.TileLeftFromGameCenter(gameCenter);
            Assert.Equal(tileLeft, actual, 12);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void DeviceEntry_VisualCenterAndParticlesFollowLogicalPoint(int direction)
        {
            ProbeGrid grid = new ProbeGrid();
            FlowSpawner flows = new FlowSpawner();
            FlowManager motion = new FlowManager(grid, flows);
            int dx = FlowMotionData.StepX(direction);
            int dy = FlowMotionData.StepY(direction);
            FlowEntity entity = new FlowEntity(
                new FlowPos(3d - dx * 0.51d, 3d - dy * 0.51d),
                new FlowMotionData(direction, 1d), TestFlow.Make(10d, 20d));
            flows.Accept(entity);

            motion.Tick(1.02d, flows);

            Assert.Equal(1, grid.Reactor.Calls);
            Assert.Equal(3, (int)Math.Floor(entity.Position.X));
            Assert.Equal(3, (int)Math.Floor(entity.Position.Y));
            double tileX = FlowPixelMath.TileLeftFromGameCenter(entity.Position.X + 0.5d);
            double tileY = FlowPixelMath.TileLeftFromGameCenter(entity.Position.Y + 0.5d);
            Assert.InRange(tileX, 3d, 4d);
            Assert.InRange(tileY, 3d, 4d);

            FlowFrameInput snapshot = new FlowFrameInput(tileX, tileY, direction, 1d,
                FlowComposition.PureWater, FlowTemp.Normal, FlowContent.Large, FlowState.Flow);
            FlowParticleSystem particles = new FlowParticleSystem();
            particles.Add(entity);
            particles.Update(entity, in snapshot, 0.125f);
            List<FlowAmbientParticle> dots = new List<FlowAmbientParticle>();
            particles.CompleteFrame(0f, dots);
            FlowAmbientParticle born = Assert.Single(dots);
            Assert.InRange((double)born.X, tileX, tileX + 1d);
            Assert.InRange((double)born.Y, tileY, tileY + 1d);
            Assert.InRange(Math.Abs(born.X - entity.Position.X), 0d, 1d);
            Assert.InRange(Math.Abs(born.Y - entity.Position.Y), 0d, 1d);
        }

        private sealed class ProbeGrid : IFlowGrid
        {
            public FlowCellKind GetCellKind(int x, int y) => FlowCellKind.Empty;
            public readonly ProbeReactor Reactor = new ProbeReactor();

            public bool Contains(int cellX, int cellY) => true;
            public IFlowRouter GetRouterAt(int cellX, int cellY) => null;
            public IFlowDevice GetReactorAt(int cellX, int cellY)
                => cellX == 3 && cellY == 3 ? Reactor : null;
        }

        private sealed class ProbeReactor : IFlowDevice
        {
            public int Calls;

            public bool TryReact(FlowEntity entity)
            {
                Calls++;
                return true;
            }
        }
    }
}
