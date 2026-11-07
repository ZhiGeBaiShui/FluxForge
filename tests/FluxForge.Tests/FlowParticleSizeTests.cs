using Xunit;

namespace FluxForge.Tests
{
    public class FlowParticleSizeTests
    {
        [Fact]
        public void RepresentativeSizesMatchTable()
        {
            Assert.Equal(10d, FlowParticleSizeConstants.Water);
            Assert.Equal(12d, FlowParticleSizeConstants.HeavyMetal);
            Assert.Equal(25d, FlowParticleSizeConstants.Microplastic);
            Assert.Equal(40d, FlowParticleSizeConstants.OrganicWaste);
            Assert.Equal(50d, FlowParticleSizeConstants.Microbe);
            Assert.Equal(100d, FlowParticleSizeConstants.PhysicalContaminant);
            Assert.Equal(200d, FlowParticleSizeConstants.DreamCapMush);
            Assert.Equal(200d, FlowParticleSizeConstants.Debris);
            Assert.Equal(5d, FlowParticleSizeConstants.Nomore);
        }
    }
}
