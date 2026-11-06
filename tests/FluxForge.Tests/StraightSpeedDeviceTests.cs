using System;
using System.Reflection;
using FluxForge;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 直通变速装置：只对自身轴线方向上的流源加减速，不改变方向、不截停。
    /// </summary>
    public class StraightSpeedDeviceTests
    {
        private static FlowEntity MakeEntity(int direction, double speed)
        {
            FlowProperties properties = new FlowProperties();
            properties.Set(FlowProps.Water, 100d);
            properties.Set(FlowProps.Temperature, 20d);

            return new FlowEntity(
                new FlowPos(2d, 2d),
                new FlowMotionData(direction, speed),
                properties);
        }

        [Fact]
        public void UpwardFlow_Accelerates()
        {
            FlowEntity entity = MakeEntity(0, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1.5d, 10d, 0d);

            bool blocked = device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.False(blocked);
            Assert.Equal(3.5d, entity.Motion.Speed);
        }

        [Fact]
        public void DownwardFlow_Decelerates()
        {
            FlowEntity entity = MakeEntity(2, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1.5d, 10d, 0d);

            device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.Equal(0.5d, entity.Motion.Speed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        public void HorizontalFlow_IsIgnoredAtFacingZero(int direction)
        {
            FlowEntity entity = MakeEntity(direction, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1.5d, 10d, 0d);

            device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.Equal(2d, entity.Motion.Speed);
        }

        [Fact]
        public void FacingOne_MapsHorizontalAxisAndKeepsLocalDirections()
        {
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);
            device.Facing = 1;

            // 世界向右 = 局部向上 → 加速
            FlowEntity right = MakeEntity(1, 2d);
            device.Route(right, new FlowSpawner(), 0, 0);
            Assert.Equal(3d, right.Motion.Speed);

            // 世界向左 = 局部向下 → 减速
            FlowEntity left = MakeEntity(3, 2d);
            device.Route(left, new FlowSpawner(), 0, 0);
            Assert.Equal(1d, left.Motion.Speed);

            // 世界向上 = 局部向左 → 非轴线，忽略
            FlowEntity up = MakeEntity(0, 2d);
            device.Route(up, new FlowSpawner(), 0, 0);
            Assert.Equal(2d, up.Motion.Speed);
        }

        [Fact]
        public void UpwardFlow_ClampsAtMaxSpeed()
        {
            FlowEntity entity = MakeEntity(0, 9.5d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.Equal(10d, entity.Motion.Speed);
        }

        [Fact]
        public void DownwardFlow_ClampsAtMinSpeed()
        {
            FlowEntity entity = MakeEntity(2, 0.5d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.Equal(0d, entity.Motion.Speed);
        }

        [Fact]
        public void Acceleration_PlaysSpeedUpSound()
        {
            FlowEntity entity = MakeEntity(0, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.Route(entity, new FlowSpawner(), 0, 0);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(FlowSoundIds.SpeedUp, played);
        }

        [Fact]
        public void Deceleration_PlaysSpeedDownSound()
        {
            FlowEntity entity = MakeEntity(2, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.Route(entity, new FlowSpawner(), 0, 0);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(FlowSoundIds.SpeedDown, played);
        }

        [Fact]
        public void AtMaxSpeed_AccelerationPlaysNoSound()
        {
            FlowEntity entity = MakeEntity(0, 10d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.Route(entity, new FlowSpawner(), 0, 0);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(10d, entity.Motion.Speed);
            Assert.Null(played);
        }

        [Fact]
        public void AtMinSpeed_DecelerationPlaysNoSound()
        {
            FlowEntity entity = MakeEntity(2, 0d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            string played = null;
            System.Action<FlowAudioEvent> handler = evt =>
            {
                if (ReferenceEquals(evt.Source, device))
                {
                    played = evt.SoundId;
                }
            };

            FlowAudio.Played += handler;
            try
            {
                device.Route(entity, new FlowSpawner(), 0, 0);
            }
            finally
            {
                FlowAudio.Played -= handler;
            }

            Assert.Equal(0d, entity.Motion.Speed);
            Assert.Null(played);
        }

        [Fact]
        public void Route_DoesNotChangeDirectionOrProperties()
        {
            FlowEntity entity = MakeEntity(0, 2d);
            StraightSpeedDevice device = new StraightSpeedDevice(1d, 10d, 0d);

            device.Route(entity, new FlowSpawner(), 0, 0);

            Assert.Equal(0, entity.Motion.Direction);
            Assert.Equal(2d, entity.Position.X);
            Assert.Equal(2d, entity.Position.Y);
            Assert.Equal(100d, entity.Properties.Get(FlowProps.Water));
            Assert.Equal(20d, entity.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void HasNoParameterlessConstructor()
        {
            ConstructorInfo constructor = typeof(StraightSpeedDevice)
                .GetConstructor(Type.EmptyTypes);

            Assert.Null(constructor);
        }
    }
}
