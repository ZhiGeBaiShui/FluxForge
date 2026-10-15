using Xunit;

namespace ProbablyBagMod.Tests
{
    public class SplitDeviceTests
    {
        public SplitDeviceTests()
        {
            // 单例状态跨用例残留，每个用例前清空就绪区
            FlowSpawner.Instance.Ready.Clear();
        }

        private static FlowEntity MakeEntity(double moisture, double temperature, int direction, float speed = 1f)
        {
            return new FlowEntity(
                new FlowPos(0f, 0f),
                new FlowMotionData { Direction = direction, Speed = speed },
                new FlowProperties(moisture, temperature));
        }

        private static void PumpReady()
        {
            FlowSpawner.Instance.Advance(0.016d);   // 喂一帧间隔，delay=0 的出队
        }

        [Fact]
        public void Route_TwoExits_RegistersTwoEntities()
        {
            // 朝右走的流源，装置：入口{右}、出口{上,下}
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 0, 2 });
            FlowEntity incoming = MakeEntity(100, 20, direction: 2);

            device.Route(incoming);
            PumpReady();

            Assert.Equal(2, FlowSpawner.Instance.Ready.Count);
        }

        [Fact]
        public void Route_TwoExits_SplitsPropertiesEvenly()
        {
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 0, 2 });
            device.Route(MakeEntity(100, 20, direction: 2));
            PumpReady();

            foreach (FlowEntity entity in FlowSpawner.Instance.Ready)
            {
                Assert.Equal(50, entity.Properties.Moisture);   // 100 平分
                Assert.Equal(20, entity.Properties.Temperature); // Keep：每份保留
            }
        }

        [Fact]
        public void Route_ExitDirectionsFollowExits()
        {
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 0, 2 });
            device.Route(MakeEntity(100, 20, direction: 2));
            PumpReady();

            // 出口集 {0=上, 2=下}，Facing=0（朝上）不旋转
            Assert.Contains(FlowSpawner.Instance.Ready, e => e.Motion.Direction == 0);
            Assert.Contains(FlowSpawner.Instance.Ready, e => e.Motion.Direction == 2);
        }

        [Fact]
        public void Route_FacingRotates_ExitsRotateWith()
        {
            // Facing=1（朝右）：入口变量 2 右旋后=绝对 3（接受朝右走的流源）；
            // 出口 {0,2} 右旋 90° 变成 {1,3}
            SplitDevice device = new SplitDevice(1, new[] { 2 }, new[] { 0, 2 });
            device.Route(MakeEntity(100, 20, direction: 3));
            PumpReady();

            Assert.Contains(FlowSpawner.Instance.Ready, e => e.Motion.Direction == 1);
            Assert.Contains(FlowSpawner.Instance.Ready, e => e.Motion.Direction == 3);
        }

        [Fact]
        public void Route_SingleExit_PassesThroughUnsplit()
        {
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 2 });
            device.Route(MakeEntity(100, 20, direction: 2));
            PumpReady();

            FlowEntity only = Assert.Single(FlowSpawner.Instance.Ready);
            Assert.Equal(100, only.Properties.Moisture);   // 一份，不平分
            Assert.Equal(2, only.Motion.Direction);
        }

        [Fact]
        public void Route_EntryMismatch_RegistersNothing()
        {
            // 入口只收「朝右」，来的流源朝上
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 0, 2 });
            device.Route(MakeEntity(100, 20, direction: 0));
            PumpReady();

            Assert.Empty(FlowSpawner.Instance.Ready);
        }

        [Fact]
        public void Route_InheritsSpeedAndPosition()
        {
            SplitDevice device = new SplitDevice(0, new[] { 2 }, new[] { 0, 2 });
            device.Route(MakeEntity(100, 20, direction: 2, speed: 2.5f));
            PumpReady();

            foreach (FlowEntity entity in FlowSpawner.Instance.Ready)
            {
                Assert.Equal(2.5f, entity.Motion.Speed);
                Assert.Equal(0f, entity.Position.X);   // 出生在流入流源的位置
                Assert.Equal(0f, entity.Position.Y);
            }
        }
    }
}
