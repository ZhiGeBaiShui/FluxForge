using Xunit;

namespace FluxForge.Tests
{
    public class TemperatureChangeReactionTests
    {
        [Fact]
        public void React_PositiveDelta_RaisesTemperature()
        {
            FlowProperties p = TestFlow.Make(100, 20);
            TemperatureChangeReaction.React(p, 30);
            Assert.Equal(50, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void React_NegativeDelta_LowersTemperature()
        {
            FlowProperties p = TestFlow.Make(100, 20);
            TemperatureChangeReaction.React(p, -15);
            Assert.Equal(5, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void React_ZeroDelta_NoChange()
        {
            FlowProperties p = TestFlow.Make(100, 20);
            TemperatureChangeReaction.React(p, 0);
            Assert.Equal(20, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void React_DoesNotTouchOtherFields()
        {
            FlowProperties p = TestFlow.Make(100, 20);
            TemperatureChangeReaction.React(p, 30);
            Assert.Equal(100, p.Get(FlowProps.Water));
        }
    }
}
