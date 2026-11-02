using System;
using System.Linq;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// Split 变体的行为矩阵（docs/device-binding-design.md §6.1）：
    /// Turn / TJunction / DoubleEmit 的真值表、AnySplit 的任意配置回归、
    /// Facing 校验与朝向换算。
    ///
    /// 全部直接调用 <see cref="SplitDevice.Route"/>，用 <see cref="FlowSpawner.Active"/>
    /// 断言出生结果：不依赖视觉层、游戏类型或运动层时序。
    /// 网格约定与游戏一致：x 向右递增、y 向下递增，方向 0=上 1=右 2=下 3=左。
    /// </summary>
    public class SplitVariantTests
    {
        private const int DeviceCellX = 4;
        private const int DeviceCellY = 6;
        private const double DeviceCenterX = DeviceCellX;
        private const double DeviceCenterY = DeviceCellY;

        /// <summary>造一个已纳入名单的流源；默认 100 水 @20℃，速度 1 格/秒。</summary>
        private static (FlowEntity Incoming, FlowSpawner Flows) Start(
            int direction, double water = 100d, double speed = 1d)
        {
            FlowEntity incoming = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(direction, speed),
                TestFlow.Make(water, 20d));

            FlowSpawner flows = new FlowSpawner();
            flows.Accept(incoming);
            return (incoming, flows);
        }

        private static void AssertBornOnDeviceCell(FlowEntity entity)
        {
            Assert.Equal(DeviceCenterX, entity.Position.X);
            Assert.Equal(DeviceCenterY, entity.Position.Y);
        }

        private static void AssertExitDirections(FlowSpawner flows, params int[] expected)
        {
            int[] actual = flows.Active.Select(entity => entity.Motion.Direction).ToArray();
            int[] sortedExpected = (int[])expected.Clone();
            Array.Sort(actual);
            Array.Sort(sortedExpected);
            Assert.Equal(sortedExpected, actual);
        }

        private static void AssertWaterConserved(FlowSpawner flows, double water)
        {
            Assert.Equal(water, flows.Active.Sum(entity => entity.Properties.Get(FlowProps.Water)), 9);
        }

        // ── TurnDevice：2 通道 { 左(3), 下(2) }，total 恒为 1 = 转向而非分流 ──

        [Fact]
        public void Turn_RightIn_ExitsDownInPlace()
        {
            // Facing 0：向右(1)进入 → 入口通道是反方向的左(3)，出口下(2)
            TurnDevice turn = new TurnDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(1);

            Assert.True(turn.Route(incoming, flows, DeviceCellX, DeviceCellY));

            // total == 1：不新建流源，流源身份与位置都不变，只改方向
            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(2, incoming.Motion.Direction);
            Assert.Equal(0.5d, incoming.Position.X);
            Assert.Equal(0.5d, incoming.Position.Y);
            Assert.Equal(100d, incoming.Properties.Get(FlowProps.Water));
            Assert.Equal(20d, incoming.Properties.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Turn_UpIn_ExitsLeftInPlace()
        {
            // 向上(0)进入 → 入口通道下(2)，出口左(3)
            TurnDevice turn = new TurnDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(0);

            Assert.True(turn.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(3, incoming.Motion.Direction);
            Assert.Equal(0.5d, incoming.Position.X);
            Assert.Equal(0.5d, incoming.Position.Y);
        }

        [Theory]
        [InlineData(2)]   // 向下：入口通道需要是上(0)，通道集没有
        [InlineData(3)]   // 向左：入口通道需要是右(1)，通道集没有
        public void Turn_DownOrLeftIn_PassesThrough(int direction)
        {
            TurnDevice turn = new TurnDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(direction);

            // 不是本装置的入口方向 → Route 返回 false，运动层按普通移动处理
            Assert.False(turn.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(direction, incoming.Motion.Direction);
            Assert.Equal(0.5d, incoming.Position.X);
            Assert.Equal(0.5d, incoming.Position.Y);
            Assert.Equal(100d, incoming.Properties.Get(FlowProps.Water));
        }

        // ── TJunctionDevice：3 通道 { 上(0), 右(1), 左(3) }，每个通道 1 道 ──

        [Theory]
        [InlineData(1, 0, 1)]   // 向右进 → 上 + 右
        [InlineData(2, 1, 3)]   // 向下进 → 右 + 左
        [InlineData(3, 0, 3)]   // 向左进 → 上 + 左
        public void TJunction_ReceivesFromThreeSides_SplitsTwoWays(
            int direction, int exitA, int exitB)
        {
            TJunctionDevice junction = new TJunctionDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(direction);

            Assert.True(junction.Route(incoming, flows, DeviceCellX, DeviceCellY));

            // 两路各 1/2，原流源被终结
            Assert.DoesNotContain(incoming, flows.Active);
            Assert.Equal(2, flows.Active.Count);
            AssertExitDirections(flows, exitA, exitB);
            AssertWaterConserved(flows, 100d);

            foreach (FlowEntity child in flows.Active)
            {
                AssertBornOnDeviceCell(child);
                Assert.Equal(1d, child.Motion.Speed);
                // 50 = 100 / 2；温度按 Keep 不随分流改变
                Assert.Equal(50d, child.Properties.Get(FlowProps.Water));
                Assert.Equal(20d, child.Properties.Get(FlowProps.Temperature));
                Assert.NotSame(incoming.Properties, child.Properties);
            }
        }

        [Fact]
        public void TJunction_UpIn_PassesThrough()
        {
            // 向上(0)进入需要入口通道下(2)，T 型缺下口 → 穿过
            TJunctionDevice junction = new TJunctionDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(0);

            Assert.False(junction.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(0, incoming.Motion.Direction);
            Assert.Equal(0.5d, incoming.Position.X);
            Assert.Equal(0.5d, incoming.Position.Y);
        }

        [Fact]
        public void TJunction_FacingOne_RotatesExitsImmediately()
        {
            // Facing 0 时向上穿过；同一实例改 Facing = 1 后通道右旋为 { 右(1), 下(2), 上(0) }，
            // 向上进入（入口通道下(2)）立刻变成「上 + 右」两路。赋值必须立即生效。
            TJunctionDevice junction = new TJunctionDevice();
            (FlowEntity passThrough, FlowSpawner passFlows) = Start(0);
            Assert.False(junction.Route(passThrough, passFlows, DeviceCellX, DeviceCellY));

            junction.Facing = 1;
            (FlowEntity incoming, FlowSpawner flows) = Start(0);
            Assert.True(junction.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Equal(2, flows.Active.Count);
            AssertExitDirections(flows, 0, 1);
            AssertWaterConserved(flows, 100d);
        }

        // ── DoubleEmitDevice：2 通道 { 上(0), 下(2) }，每个通道 2 道 ──

        [Fact]
        public void DoubleEmit_UpIn_EmitsTwoUp()
        {
            DoubleEmitDevice device = new DoubleEmitDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(0, speed: 2d);

            Assert.True(device.Route(incoming, flows, DeviceCellX, DeviceCellY));

            // 入口通道下(2)也配了 2 道，但它不参与计算；若参与，total 会是 4。
            Assert.DoesNotContain(incoming, flows.Active);
            Assert.Equal(2, flows.Active.Count);
            AssertExitDirections(flows, 0, 0);
            AssertWaterConserved(flows, 100d);

            FlowEntity first = flows.Active[0];
            FlowEntity second = flows.Active[1];
            foreach (FlowEntity child in flows.Active)
            {
                AssertBornOnDeviceCell(child);
                Assert.Equal(0, child.Motion.Direction);
                Assert.Equal(2d, child.Motion.Speed);
                Assert.Equal(50d, child.Properties.Get(FlowProps.Water));
                Assert.Equal(20d, child.Properties.Get(FlowProps.Temperature));
                Assert.NotSame(incoming.Properties, child.Properties);
            }

            // 同口双发的两颗子流源完全重合：同格、同向、同速
            Assert.Equal(first.Position.X, second.Position.X);
            Assert.Equal(first.Position.Y, second.Position.Y);
            Assert.Equal(first.Motion.Direction, second.Motion.Direction);
            Assert.Equal(first.Motion.Speed, second.Motion.Speed);
            Assert.NotSame(first, second);
            Assert.NotSame(first.Properties, second.Properties);
        }

        [Fact]
        public void DoubleEmit_DownIn_EmitsTwoDown()
        {
            // 双向双发：从下口进入也能双发，不会因为 Facing 未同步而静默吞流
            DoubleEmitDevice device = new DoubleEmitDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(2);

            Assert.True(device.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.DoesNotContain(incoming, flows.Active);
            Assert.Equal(2, flows.Active.Count);
            AssertExitDirections(flows, 2, 2);
            AssertWaterConserved(flows, 100d);

            foreach (FlowEntity child in flows.Active)
            {
                AssertBornOnDeviceCell(child);
                Assert.Equal(2, child.Motion.Direction);
                Assert.Equal(50d, child.Properties.Get(FlowProps.Water));
            }
        }

        [Theory]
        [InlineData(1)]   // 向右：入口通道需要是左(3)
        [InlineData(3)]   // 向左：入口通道需要是右(1)
        public void DoubleEmit_LeftOrRightIn_PassesThrough(int direction)
        {
            DoubleEmitDevice device = new DoubleEmitDevice();
            (FlowEntity incoming, FlowSpawner flows) = Start(direction);

            Assert.False(device.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Same(incoming, Assert.Single(flows.Active));
            Assert.Equal(direction, incoming.Motion.Direction);
            Assert.Equal(0.5d, incoming.Position.X);
            Assert.Equal(0.5d, incoming.Position.Y);
        }

        [Fact]
        public void EntryEmitCount_IsIgnored()
        {
            // 设计稿 §6.1：入口通道写 5、出口写 1，仍只发 1 道。
            // DoubleEmit 的常量是 { 2, 2 }，这里用 AnySplit 复刻同形配置 { 上(0), 下(2) }。
            AnySplit split = new AnySplit(new[] { 0, 2 }, new[] { 5, 1 });

            // 向上进入：入口通道下(2)的计数 1 被忽略，出口上(0)发 5 道
            (FlowEntity upIn, FlowSpawner upFlows) = Start(0);
            Assert.True(split.Route(upIn, upFlows, DeviceCellX, DeviceCellY));
            Assert.Equal(5, upFlows.Active.Count);
            AssertExitDirections(upFlows, 0, 0, 0, 0, 0);
            AssertWaterConserved(upFlows, 100d);
            Assert.All(upFlows.Active, child =>
                Assert.Equal(20d, child.Properties.Get(FlowProps.Water)));

            // 向下进入：入口通道上(0)的计数 5 被忽略，出口下(2)只发 1 道
            (FlowEntity downIn, FlowSpawner downFlows) = Start(2);
            Assert.True(split.Route(downIn, downFlows, DeviceCellX, DeviceCellY));
            Assert.Same(downIn, Assert.Single(downFlows.Active));
            Assert.Equal(2, downIn.Motion.Direction);
            Assert.Equal(100d, downIn.Properties.Get(FlowProps.Water));
        }

        // ── AnySplit：任意通道 / 任意发射数量的回归 ──

        [Fact]
        public void AnySplit_AllExitsZero_KillsIncoming()
        {
            // 入口通道 3 的计数 1 被忽略；唯一出口下(2)发 0 道 → 全吸收
            AnySplit split = new AnySplit(new[] { 3, 2 }, new[] { 1, 0 });
            (FlowEntity incoming, FlowSpawner flows) = Start(1);

            Assert.True(split.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.Empty(flows.Active);   // Kill：名单清空
            Assert.DoesNotContain(incoming, flows.Active);
        }

        [Fact]
        public void AnySplit_MixedCounts_EmitsThreeWithTwoPlusOne()
        {
            // 通道 { 下(2), 右(1), 左(3) }，发射数 { 0, 2, 1 }：
            // 向上进入（入口通道下(2)，计数 0 被忽略）→ 出口右发 2 道、左发 1 道，共 3 道。
            AnySplit split = new AnySplit(new[] { 2, 1, 3 }, new[] { 0, 2, 1 });
            (FlowEntity incoming, FlowSpawner flows) = Start(0);

            Assert.True(split.Route(incoming, flows, DeviceCellX, DeviceCellY));

            Assert.DoesNotContain(incoming, flows.Active);
            Assert.Equal(3, flows.Active.Count);
            AssertExitDirections(flows, 1, 1, 3);
            AssertWaterConserved(flows, 100d);

            foreach (FlowEntity child in flows.Active)
            {
                AssertBornOnDeviceCell(child);
                Assert.Equal(100d / 3d, child.Properties.Get(FlowProps.Water), 9);
                Assert.Equal(20d, child.Properties.Get(FlowProps.Temperature));
            }

            // 2 道的出口拿 2/3、1 道的出口拿 1/3
            Assert.Equal(200d / 3d, flows.Active
                .Where(entity => entity.Motion.Direction == 1)
                .Sum(entity => entity.Properties.Get(FlowProps.Water)), 9);
            Assert.Equal(100d / 3d, flows.Active
                .Where(entity => entity.Motion.Direction == 3)
                .Sum(entity => entity.Properties.Get(FlowProps.Water)), 9);
        }

        // ── 构造校验与 Facing ──

        [Fact]
        public void AnySplit_NullChannelDirs_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AnySplit(null));
        }

        [Fact]
        public void AnySplit_LessThanTwoChannels_Throws()
        {
            // 至少 1 个入口 + 1 个出口
            Assert.Throws<ArgumentException>(() => new AnySplit(new[] { 1 }));
        }

        [Fact]
        public void AnySplit_DuplicateChannelDirection_Throws()
        {
            // 重复方向会让入口被跳过两次、少发流源；同口双发只能靠 emitCounts
            Assert.Throws<ArgumentException>(() => new AnySplit(new[] { 1, 1 }));
        }

        [Fact]
        public void AnySplit_EmitCountsLengthMismatch_Throws()
        {
            Assert.Throws<ArgumentException>(() => new AnySplit(new[] { 0, 2 }, new[] { 1 }));
        }

        [Fact]
        public void AnySplit_NegativeEmitCount_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnySplit(new[] { 0, 2 }, new[] { -1, 1 }));
        }

        [Fact]
        public void AnySplit_DirectionOutOfRange_Throws()
        {
            // 超范围方向若被 % 4 悄悄折回，会变成另一个合法方向
            Assert.Throws<ArgumentOutOfRangeException>(() => new AnySplit(new[] { 0, 4 }));
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(4)]
        public void Facing_OutOfRange_ThrowsAndKeepsPreviousValue(int facing)
        {
            TurnDevice turn = new TurnDevice();
            turn.Facing = 2;

            Assert.Throws<ArgumentOutOfRangeException>(() => turn.Facing = facing);

            // 校验发生在赋值之前，非法写入不能污染已有朝向
            Assert.Equal(2, turn.Facing);
        }
    }
}
