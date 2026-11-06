using System.Collections.Generic;
using FluxForge;
using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 朝向是独立能力接口：路由和反应装置都可以实现。
    /// </summary>
    public class FacingInterfaceTests
    {
        [Fact]
        public void FlowRouter_DoesNotImplyFacing()
        {
            Assert.False(typeof(IFacing).IsAssignableFrom(typeof(IFlowRouter)));
        }

        [Fact]
        public void RoutingDevices_ThatNeedFacing_ImplementIt()
        {
            Assert.True(typeof(IFacing).IsAssignableFrom(typeof(SplitDevice)));
            Assert.True(typeof(IFacing).IsAssignableFrom(typeof(CollectorDevice)));
            Assert.True(typeof(IFacing).IsAssignableFrom(typeof(StraightSpeedDevice)));
        }

        [Fact]
        public void DeviceHost_BroadcastsFacingToAllFacingDevices()
        {
            DeviceStateAdapter adapter = DeviceAdapterCatalog.Get("turn").Adapter;
            DeviceHost host = DeviceHost.Create(
                new GameItem(),
                new List<DeviceDefinition>
                {
                    new DeviceDefinition("turn_a", adapter),
                    new DeviceDefinition("turn_b", adapter)
                });

            Assert.Equal(2, host.Facings.Count);

            host.SetFacing(2, true);

            for (int i = 0; i < host.Facings.Count; i++)
            {
                Assert.Equal(2, host.Facings[i].Facing);
                Assert.True(host.Facings[i].Flipped);
            }
        }

        [Fact]
        public void Reactor_CanImplementFacing()
        {
            FacingReactor reactor = new FacingReactor();
            IFacing facing = reactor;

            facing.Facing = 3;
            facing.Flipped = true;

            Assert.Equal(3, reactor.Facing);
            Assert.True(reactor.Flipped);
        }

        private sealed class FacingReactor : IFlowDevice, IFacing
        {
            public int Facing { get; set; }
            public bool Flipped { get; set; }

            public bool TryReact(FlowEntity entity)
            {
                return false;
            }
        }
    }
}
