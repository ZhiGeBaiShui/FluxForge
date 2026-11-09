using Xunit;

namespace FluxForge.Tests
{
    public class FlowReactionDelayTests
    {
        [Fact]
        public void RandomDelay_IsWithinThreeTenthsAndVisible()
        {
            for (int i = 0; i < 128; i++)
            {
                Assert.InRange(
                    FlowReactionDelay.NextSeconds(),
                    0.05d,
                    FlowReactionDelay.MaxSeconds);
            }
        }
    }
}
