using Xunit;

namespace FluxForge.Tests
{
    public class CentrifugeDeviceTests
    {
        private static FlowEntity Start(
            double organicWaste,
            double chemical,
            double speed)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.OrganicWaste, organicWaste);
            properties.Set(FlowProps.ChemicalContaminant, chemical);
            properties.Set(FlowProps.Temperature, 20d);

            return new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, speed),
                properties);
        }

        [Fact]
        public void K2_ExtractsEligibleOrganicWasteIntoItself()
        {
            FlowEntity incoming = Start(100d, 20d, 5d);
            CentrifugeDevice device = new CentrifugeDevice(2d, 1000);

            // speed 5 -> threshold 40；有机废物 40 可分离，化学污染物 2 不可分离。
            // x=100, y=20, k=2 -> removable=60
            Assert.False(device.TryReact(incoming));

            Assert.Equal(40d, incoming.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(20d, incoming.Properties.Get(FlowProps.ChemicalContaminant), 9);
            Assert.Equal(60d, device.Extracted.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(0d, device.Extracted.Get(FlowProps.ChemicalContaminant), 9);
        }

        [Fact]
        public void CapacityLimitsExtraction()
        {
            FlowEntity incoming = Start(100d, 20d, 5d);
            CentrifugeDevice device = new CentrifugeDevice(2d, 30);

            Assert.False(device.TryReact(incoming));

            Assert.Equal(70d, incoming.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(30d, device.Extracted.Get(FlowProps.OrganicWaste), 9);
        }

        [Fact]
        public void NotEnoughEligibleMass_DoesNothing()
        {
            FlowEntity incoming = Start(10d, 20d, 5d);
            CentrifugeDevice device = new CentrifugeDevice(2d, 1000);

            // x=10, y=20, k=2 -> removable=-30，不能分离。
            Assert.False(device.TryReact(incoming));

            Assert.Equal(10d, incoming.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(20d, incoming.Properties.Get(FlowProps.ChemicalContaminant), 9);
            Assert.Equal(0d, device.Extracted.SubstanceTotal, 9);
        }

        [Fact]
        public void SlowerSpeed_ExcludesSmallerParticles()
        {
            FlowEntity incoming = Start(100d, 0d, 4d);

            // speed 4 -> threshold 50；有机废物 40 不再可分离。
            CentrifugeDevice device = new CentrifugeDevice(2d, 1000);

            Assert.False(device.TryReact(incoming));
            Assert.Equal(100d, incoming.Properties.Get(FlowProps.OrganicWaste), 9);
            Assert.Equal(0d, device.Extracted.SubstanceTotal, 9);
        }

        [Fact]
        public void HighK_RequiresCleanerFeed()
        {
            FlowEntity incoming = Start(90d, 10d, 5d);

            // x=90, y=10, k=10 -> removable=-10，不能分离。
            CentrifugeDevice device = new CentrifugeDevice(10d, 1000);

            Assert.False(device.TryReact(incoming));
            Assert.Equal(0d, device.Extracted.SubstanceTotal, 9);
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
