using System.Linq;
using Xunit;

namespace FluxForge.Tests
{
    public class CentrifugeDeviceTests
    {
        private const int DeviceX = 4;
        private const int DeviceY = 6;

        private static FlowEntity Start(
            FlowSpawner flows,
            double organicWaste,
            double chemical,
            double speed)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.OrganicWaste, organicWaste);
            properties.Set(FlowProps.ChemicalContaminant, chemical);
            properties.Set(FlowProps.Temperature, 20d);

            FlowEntity incoming = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, speed),
                properties);

            flows.Accept(incoming);
            return incoming;
        }

        [Fact]
        public void K2_SplitsEligibleOrganicWaste()
        {
            FlowSpawner flows = new FlowSpawner();
            FlowEntity incoming = Start(flows, 100d, 20d, 5d);

            // speed 5 -> threshold 40；有机废物 40 可分离，化学污染物 2 不可分离。
            CentrifugeDevice device = new CentrifugeDevice(2d, 1, 2);

            Assert.True(device.Route(incoming, flows, DeviceX, DeviceY));
            Assert.DoesNotContain(incoming, flows.Active);
            Assert.Equal(2, flows.Active.Count);

            FlowEntity heavy = flows.Active.Single(
                entity => entity.Motion.Direction == 2);
            FlowEntity light = flows.Active.Single(
                entity => entity.Motion.Direction == 1);

            // x=100, y=20, k=2 -> removable=60
            Assert.Equal(60d, heavy.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(0d, heavy.Properties.Get(FlowProps.ChemicalContaminant), 9);
            Assert.Equal(40d, light.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(20d, light.Properties.Get(FlowProps.ChemicalContaminant), 9);
            Assert.Equal(20d, heavy.Properties.Get(FlowProps.Temperature), 9);
            Assert.Equal(20d, light.Properties.Get(FlowProps.Temperature), 9);
        }

        [Fact]
        public void NotEnoughEligibleMass_PassesThrough()
        {
            FlowSpawner flows = new FlowSpawner();
            FlowEntity incoming = Start(flows, 10d, 20d, 5d);

            CentrifugeDevice device = new CentrifugeDevice(2d, 1, 2);

            // x=10, y=20, k=2 -> removable=-30，不能分离。
            Assert.False(device.Route(incoming, flows, DeviceX, DeviceY));
            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(10d, incoming.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(20d, incoming.Properties.Get(FlowProps.ChemicalContaminant), 9);
        }

        [Fact]
        public void SlowerSpeed_ExcludesSmallerParticles()
        {
            FlowSpawner flows = new FlowSpawner();
            FlowEntity incoming = Start(flows, 100d, 0d, 4d);

            // speed 4 -> threshold 50；有机废物 40 不再可分离。
            CentrifugeDevice device = new CentrifugeDevice(2d, 1, 2);

            Assert.False(device.Route(incoming, flows, DeviceX, DeviceY));
            Assert.Same(incoming, Assert.Single(flows.Active));
        }

        [Fact]
        public void HighK_RequiresCleanerFeed()
        {
            FlowSpawner flows = new FlowSpawner();
            FlowEntity incoming = Start(flows, 90d, 10d, 5d);

            // x=90, y=10, k=10 -> removable=-10，不能分离。
            CentrifugeDevice device = new CentrifugeDevice(10d, 1, 2);

            Assert.False(device.Route(incoming, flows, DeviceX, DeviceY));
            Assert.Same(incoming, Assert.Single(flows.Active));
        }

        [Fact]
        public void ThresholdForSpeed_IsClampedBetween25And200()
        {
            Assert.Equal(200d, CentrifugeDevice.ThresholdForSpeed(0.1d));
            Assert.Equal(100d, CentrifugeDevice.ThresholdForSpeed(2d));
            Assert.Equal(40d, CentrifugeDevice.ThresholdForSpeed(5d));
            Assert.Equal(25d, CentrifugeDevice.ThresholdForSpeed(10d));
        }
    }
}
