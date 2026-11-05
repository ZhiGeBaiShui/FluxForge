using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 装置在动作点直接向 Logic 的 FlowAudio / FlowAnimation 播报语义 ID。
    /// </summary>
    public class FlowPresentationTests
    {
        [Fact]
        public void FilterPublishesOneCleanedSound()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 100d);
            properties.Set(FlowProps.ChemicalContaminant, 500d);
            properties.Set(FlowProps.HeavyMetal, 500d);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                properties);

            FilterDevice filter = new FilterDevice();
            int count = 0;
            string soundId = null;
            object source = null;
            int animationCount = 0;

            System.Action<FlowAudioEvent> audioHandler = evt =>
            {
                count++;
                soundId = evt.SoundId;
                source = evt.Source;
            };
            System.Action<FlowAnimationEvent> animationHandler = evt => animationCount++;

            FlowAudio.Played += audioHandler;
            FlowAnimation.Played += animationHandler;
            filter.TryReact(entity);
            FlowAudio.Played -= audioHandler;
            FlowAnimation.Played -= animationHandler;

            Assert.Equal(1, count);
            Assert.Equal(1, animationCount);
            Assert.Equal(FlowSoundIds.FilterCleaned, soundId);
            Assert.Same(filter, source);
        }

        [Fact]
        public void TemperaturePublishesHeatedOrCooled()
        {
            TemperatureDevice device = new TemperatureDevice(0d);
            device.Temperature = -12d;

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                new FlowProperties());

            string soundId = null;
            double amount = 0d;
            object source = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                soundId = evt.SoundId;
                amount = evt.Amount;
                source = evt.Source;
            };

            FlowAudio.Played += handler;
            device.TryReact(entity);
            FlowAudio.Played -= handler;

            Assert.Equal(FlowSoundIds.Cooled, soundId);
            Assert.Equal(12d, amount);
            Assert.Same(device, source);
        }

        [Fact]
        public void TemperaturePublishesHeatedWhenPositive()
        {
            TemperatureDevice device = new TemperatureDevice(0d);
            device.Temperature = 12d;

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                new FlowProperties());

            string soundId = null;
            System.Action<FlowAudioEvent> handler = evt => soundId = evt.SoundId;

            FlowAudio.Played += handler;
            device.TryReact(entity);
            FlowAudio.Played -= handler;

            Assert.Equal(FlowSoundIds.Heated, soundId);
        }

        [Fact]
        public void TemperatureDoesNotPublishAtZero()
        {
            TemperatureDevice device = new TemperatureDevice(0d);
            int count = 0;
            System.Action<FlowAudioEvent> handler = evt => count++;

            FlowAudio.Played += handler;
            device.TryReact(new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                new FlowProperties()));
            FlowAudio.Played -= handler;

            Assert.Equal(0, count);
        }

        [Fact]
        public void FilterDoesNotPublishForCleanWater()
        {
            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                TestFlow.Make(100d, 20d));

            int count = 0;
            System.Action<FlowAudioEvent> handler = evt => count++;

            FlowAudio.Played += handler;
            new FilterDevice().TryReact(entity);
            FlowAudio.Played -= handler;

            Assert.Equal(0, count);
        }

        [Fact]
        public void CollectorPublishesAbsorbed()
        {
            CollectorDevice collector = new CollectorDevice(new FlowProperties());
            FlowSpawner flows = new FlowSpawner();
            FlowEntity incoming = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(2, 2d),
                TestFlow.Make(100d, 20d));
            flows.Accept(incoming);

            string soundId = null;
            double amount = 0d;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                soundId = evt.SoundId;
                amount = evt.Amount;
            };

            FlowAudio.Played += handler;
            collector.Route(incoming, flows, 1, 1);
            FlowAudio.Played -= handler;

            Assert.Equal(FlowSoundIds.Absorbed, soundId);
            Assert.Equal(100d, amount);
        }

        [Fact]
        public void IceBlockPublishesMeltedAndExhausted()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                TestFlow.Make(100d, 25d));

            bool melted = false;
            bool exhausted = false;
            double meltedAmount = 0d;
            object source = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (evt.SoundId == FlowSoundIds.Melted)
                {
                    melted = true;
                    meltedAmount = evt.Amount;
                    source = evt.Source;
                }

                exhausted |= evt.SoundId == FlowSoundIds.Exhausted;
            };

            FlowAudio.Played += handler;
            ice.TryReact(entity);
            FlowAudio.Played -= handler;

            Assert.True(melted);
            Assert.True(exhausted);
            Assert.Equal(10d, meltedAmount);
            Assert.Same(ice, source);
        }

        [Fact]
        public void TurnDoesNotPublishAudio()
        {
            TurnDevice turn = new TurnDevice();
            FlowSpawner flows = new FlowSpawner();
            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                TestFlow.Make(100d, 20d));
            flows.Accept(entity);

            int count = 0;
            System.Action<FlowAudioEvent> handler = evt => count++;

            FlowAudio.Played += handler;
            turn.Route(entity, flows, 0, 0);
            FlowAudio.Played -= handler;

            Assert.Equal(0, count);
        }

        [Fact]
        public void SplitAbsorptionDoesNotPublishRouted()
        {
            AbsorbingSplit split = new AbsorbingSplit();
            FlowSpawner flows = new FlowSpawner();
            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(2, 2d),
                TestFlow.Make(100d, 20d));
            flows.Accept(entity);

            int count = 0;
            System.Action<FlowAudioEvent> handler = evt => count++;

            FlowAudio.Played += handler;
            split.Route(entity, flows, 0, 0);
            FlowAudio.Played -= handler;

            Assert.Equal(0, count);
        }

        private sealed class AbsorbingSplit : SplitDevice
        {
            public AbsorbingSplit()
                : base(new[] { 0, 2 }, new[] { 0, 0 })
            {
            }
        }

        [Fact]
        public void FilterAlsoPublishesAnimationId()
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Temperature, 100d);
            properties.Set(FlowProps.ChemicalContaminant, 500d);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 2d),
                properties);

            string animationId = null;
            System.Action<FlowAnimationEvent> handler = evt => animationId = evt.AnimationId;

            FlowAnimation.Played += handler;
            new FilterDevice().TryReact(entity);
            FlowAnimation.Played -= handler;

            Assert.Equal(FlowAnimationIds.FilterPulse, animationId);
        }
    }
}
