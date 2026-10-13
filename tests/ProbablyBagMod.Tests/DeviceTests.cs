using Xunit;
using ProbablyBagMod;

namespace ProbablyBagMod.Tests
{
    public class DeviceTests
    {
        private static LiquidPayload Make(double moisture)
        {
            return new LiquidPayload { Moisture = moisture };
        }

        private static FlowToken Token(double moisture, Direction dir)
        {
            return new FlowToken(new GridPos(0, 0), dir, Make(moisture));
        }

        [Fact]
        public void TurnDevice_TurnsTraffic()
        {
            var device = new TurnDevice(Direction.Left, Direction.Down);

            IReadOnlyList<FlowOutput> outs = device.OnFlowEnter(Token(100, Direction.Right), Direction.Left);

            Assert.Single(outs);
            Assert.Equal(Direction.Down, outs[0].Direction);
            Assert.Equal(100, ((LiquidPayload)outs[0].Payload).Moisture);
        }

        [Fact]
        public void TurnDevice_WrongEntry_IsAbsorbed()
        {
            var device = new TurnDevice(Direction.Left, Direction.Down);

            IReadOnlyList<FlowOutput> outs = device.OnFlowEnter(Token(100, Direction.Right), Direction.Up);

            Assert.Empty(outs);
        }

        [Fact]
        public void SplitDevice_SplitsInHalf()
        {
            var device = new SplitDevice(Direction.Left, Direction.Up, Direction.Down);

            IReadOnlyList<FlowOutput> outs = device.OnFlowEnter(Token(100, Direction.Right), Direction.Left);

            Assert.Equal(2, outs.Count);
            Assert.Equal(Direction.Up, outs[0].Direction);
            Assert.Equal(Direction.Down, outs[1].Direction);
            Assert.Equal(50, ((LiquidPayload)outs[0].Payload).Moisture);
            Assert.Equal(50, ((LiquidPayload)outs[1].Payload).Moisture);
        }

        [Fact]
        public void MergeDevice_OutputsOnlyWhenFilled()
        {
            var device = new MergeDevice(2, Direction.Right);

            IReadOnlyList<FlowOutput> first = device.OnFlowEnter(Token(40, Direction.Left), Direction.Left);
            Assert.Empty(first);   // 还差一份

            IReadOnlyList<FlowOutput> second = device.OnFlowEnter(Token(60, Direction.Up), Direction.Up);
            Assert.Single(second);
            Assert.Equal(Direction.Right, second[0].Direction);
            Assert.Equal(100, ((LiquidPayload)second[0].Payload).Moisture);
        }

        [Fact]
        public void MergeDevice_Reset_DropsBuffer()
        {
            var device = new MergeDevice(2, Direction.Right);

            device.OnFlowEnter(Token(40, Direction.Left), Direction.Left);
            device.Reset();

            IReadOnlyList<FlowOutput> after = device.OnFlowEnter(Token(60, Direction.Left), Direction.Left);
            Assert.Empty(after);   // buffer 已清空，又变成只到一份
        }

        [Fact]
        public void OutputCollector_ImplementsMarker()
        {
            Assert.True(new OutputCollector() is IFlowOutput);
        }

        [Fact]
        public void Emitter_SpawnUsesGivenDirectionAndPayload()
        {
            var emitter = new EmitterDevice(Direction.Right, () => Make(42));

            FlowToken token = emitter.Spawn(new GridPos(3, 7));

            Assert.Equal(Direction.Right, token.Direction);
            Assert.Equal(42, ((LiquidPayload)token.Payload).Moisture);
            Assert.Equal(new GridPos(3, 7), token.Position);
        }
    }
}
