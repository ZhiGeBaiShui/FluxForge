using System.Collections.Generic;
using Xunit;

namespace FluxForge.Tests
{
    /// <summary>
    /// 发射 / 收集 / 冰块三种装置的行为验证。
    ///
    /// 这几个装置都不碰游戏类型，可以直接跑——
    /// 「装置逻辑对不对」和「游戏里显示得对不对」是两件事，先在这里把前者钉死。
    /// </summary>
    public class DeviceTests
    {
        // 网格 / 生命周期 / 运动各一份，模拟一台流量机的最小装配。
        // xUnit 每个测试方法都会新建测试类实例，三者天然隔离，
        // 所以单例时代那种在每个测试开头 Reset 的写法不再需要。
        private readonly FakeGrid _grid = new FakeGrid();
        private readonly FlowSpawner _flows = new FlowSpawner();
        private readonly FlowManager _motion;

        public DeviceTests()
        {
            // 网格由构造注入：一台机器一块网格，流源不再自带网格
            _motion = new FlowManager(_grid);
        }

        /// <summary>
        /// 默认无限大、无装置的测试网格；需要装置时用 <see cref="SetRouter"/> 放，
        /// 需要验证边界回收时用 <see cref="SetBounds"/> 收紧范围。
        /// </summary>
        private sealed class FakeGrid : IFlowGrid
        {
            private readonly Dictionary<(int, int), IFlowRouter> _routers
                = new Dictionary<(int, int), IFlowRouter>();

            private bool _bounded;
            private int _minX;
            private int _minY;
            private int _maxX;
            private int _maxY;

            public FakeGrid SetRouter(int cellX, int cellY, IFlowRouter router)
            {
                _routers[(cellX, cellY)] = router;
                return this;
            }

            public FakeGrid SetBounds(int minX, int minY, int maxX, int maxY)
            {
                _bounded = true;
                _minX = minX;
                _minY = minY;
                _maxX = maxX;
                _maxY = maxY;
                return this;
            }

            public bool Contains(int cellX, int cellY)
            {
                if (!_bounded)
                {
                    return true;
                }

                return cellX >= _minX && cellY >= _minY && cellX < _maxX && cellY < _maxY;
            }

            public IFlowRouter GetRouterAt(int cellX, int cellY)
            {
                IFlowRouter router;
                return _routers.TryGetValue((cellX, cellY), out router) ? router : null;
            }

            public IFlowDevice GetReactorAt(int cellX, int cellY)
            {
                return null;
            }
        }

        /// <summary>
        /// 订阅本测试实例的出生广播。
        /// 名单归测试实例所有，不能再从单例入口挂事件，否则测试之间会互相串台。
        /// </summary>
        private List<FlowEntity> CaptureSpawned()
        {
            List<FlowEntity> spawned = new List<FlowEntity>();
            _flows.OnEntityAdded += spawned.Add;
            return spawned;
        }

        [Fact]
        public void Collector_AccumulatesAndKillsIncoming()
        {
            CollectorDevice collector = new CollectorDevice();

            collector.Route(new FlowEntity(new FlowPos(0, 0), new FlowMotionData(0, 1), TestFlow.Make(40, 10)), _flows, 0, 0);
            collector.Route(new FlowEntity(new FlowPos(0, 0), new FlowMotionData(0, 1), TestFlow.Make(60, 20)), _flows, 0, 0);

            Assert.Equal(100d, collector.Stock.Get(FlowProps.Water));
            // 加权平均：(40×10 + 60×20) / 100 = 16
            Assert.Equal(16d, collector.Stock.Get(FlowProps.Temperature), 3);

            collector.Clear();
            Assert.Equal(0d, collector.Stock.Get(FlowProps.Water));
            Assert.Equal(0d, collector.Stock.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Collector_RouteReportsChanged()
        {
            CollectorDevice collector = new CollectorDevice();
            int changedCount = 0;
            double waterAtEvent = -1d;

            collector.Changed += sender =>
            {
                changedCount++;
                waterAtEvent = ((CollectorDevice)sender).Stock.Get(FlowProps.Water);
            };

            collector.Route(new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                TestFlow.Make(40d, 10d)), _flows, 1, 0);

            Assert.Equal(1, changedCount);
            // 播报时库存已经合并完成，订阅方读到的是一次完整的吸收结果
            Assert.Equal(40d, waterAtEvent);
        }

        [Fact]
        public void TwoGrids_AdvanceIndependently()
        {
            // 网格 A 在 (1,0) 放了收集器，网格 B 同样位置是空的：
            // 两个流源同帧推进，A 的流源被吸走，B 的必须原样走完这一帧。
            // 新设计里「独立」的单位是一台流量机：各有一套网格 / 名单 / 运动器。
            CollectorDevice collector = new CollectorDevice();
            FakeGrid gridA = new FakeGrid().SetRouter(1, 0, collector);
            FakeGrid gridB = new FakeGrid();

            FlowSpawner flowsA = new FlowSpawner();
            FlowSpawner flowsB = new FlowSpawner();
            FlowManager motionA = new FlowManager(gridA);
            FlowManager motionB = new FlowManager(gridB);

            FlowEntity entityA = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                TestFlow.Make(10d, 20d));
            FlowEntity entityB = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                TestFlow.Make(10d, 20d));

            flowsA.Accept(entityA);
            flowsB.Accept(entityB);

            motionA.Tick(1d, flowsA);
            motionB.Tick(1d, flowsB);

            Assert.Equal(10d, collector.Stock.Get(FlowProps.Water));
            Assert.DoesNotContain(entityA, flowsA.Active);

            Assert.Contains(entityB, flowsB.Active);
            Assert.Equal(1.5d, entityB.Position.X, 3);
            Assert.Equal(0.5d, entityB.Position.Y, 3);
        }

        [Fact]
        public void IceBlock_TransfersWaterAndMixesTemperature()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            FlowProperties flow = TestFlow.Make(100d, 25d);

            bool reacted = ice.TryReact(flow);

            Assert.True(reacted);
            // 流源能带走自身质量的一半（50），远大于冰块剩余的 10，所以一次化完
            Assert.Equal(0d, ice.Water);
            Assert.Equal(110d, flow.Get(FlowProps.Water));
            // (100×25 + 10×(-5)) / 110 ≈ 22.273
            Assert.Equal(22.273d, flow.Get(FlowProps.Temperature), 3);
        }

        [Fact]
        public void IceBlock_SmallFlowOnlyMeltsHalfItsOwnMass()
        {
            // 一杯水化不完整块冰：流源只带 100，最多带走 50，冰还剩 950
            IceBlockDevice ice = new IceBlockDevice(1000d, -10d);
            FlowProperties flow = TestFlow.Make(100d, 20d);

            bool exhausted = false;
            ice.Exhausted += sender => exhausted = true;

            Assert.True(ice.TryReact(flow));

            Assert.Equal(950d, ice.Water);
            Assert.Equal(150d, flow.Get(FlowProps.Water));
            // (100×20 + 50×(-10)) / 150 = 10
            Assert.Equal(10d, flow.Get(FlowProps.Temperature), 3);
            Assert.False(exhausted);          // 没化完就不该请求销毁
        }

        [Fact]
        public void IceBlock_RepeatedSmallFlowsEventuallyMeltThrough()
        {
            // 连续几轮小流源应当把冰吃空，最后一轮触发销毁
            IceBlockDevice ice = new IceBlockDevice(300d, -10d);
            int exhaustedCount = 0;
            ice.Exhausted += sender => exhaustedCount++;

            int rounds = 0;
            while (ice.Water > 0d && rounds < 100)
            {
                ice.TryReact(TestFlow.Make(100d, 20d));
                rounds++;
            }

            Assert.Equal(0d, ice.Water);
            Assert.Equal(1, exhaustedCount);   // 只在真正归零的那一次触发
            Assert.True(rounds < 100);         // 不会无限循环
        }

        [Fact]
        public void IceBlock_ReportsChangedThenExhausted_StockAlreadyZero()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            List<string> order = new List<string>();
            double waterWhenChanged = -1d;
            double waterWhenExhausted = -1d;

            ice.Changed += sender =>
            {
                order.Add("Changed");
                // Changed 必须已经能读到最终值：订阅方就是靠它把 tag 写成 0 的。
                // 如果哪天把播报挪到 Water = 0 之前，这条断言会立刻失败。
                waterWhenChanged = ((IceBlockDevice)sender).Water;
            };
            ice.Exhausted += sender =>
            {
                order.Add("Exhausted");
                waterWhenExhausted = ((IceBlockDevice)sender).Water;
            };

            Assert.True(ice.TryReact(TestFlow.Make(100d, 25d)));

            // 顺序不可调换：Exhausted 的订阅方会把物品从网格摘掉，
            // Changed 必须先把「存量归零」同步出去
            Assert.Equal(new[] { "Changed", "Exhausted" }, order);
            Assert.Equal(0d, waterWhenChanged);
            Assert.Equal(0d, waterWhenExhausted);
        }

        [Fact]
        public void IceBlock_ReturnsFalseWhenMelted()
        {
            IceBlockDevice ice = new IceBlockDevice(0d, -5d);
            FlowProperties flow = TestFlow.Make(100d, 25d);

            Assert.False(ice.TryReact(flow));
            Assert.Equal(100d, flow.Get(FlowProps.Water));   // 没反应就不该动流源
            Assert.Equal(25d, flow.Get(FlowProps.Temperature));
        }

        [Fact]
        public void SameGrid_HoldsMultipleEntities_EachAdvancesIndependently()
        {
            // 一块网格里同时跑三个流源：位置不同、方向不同，谁都不该被谁挤掉。
            // 「流源不是一格一个」是这套框架的前提，发射装置一秒钟能吐好几个。
            FlowEntity goingRight = new FlowEntity(
                new FlowPos(0.5d, 0.5d), new FlowMotionData(1, 1), TestFlow.Make(10d, 20d));
            FlowEntity goingUp = new FlowEntity(
                new FlowPos(3.5d, 2.5d), new FlowMotionData(0, 1), TestFlow.Make(11d, 21d));
            FlowEntity goingLeft = new FlowEntity(
                new FlowPos(6.5d, 4.5d), new FlowMotionData(3, 1), TestFlow.Make(12d, 22d));

            _flows.Accept(goingRight);
            _flows.Accept(goingUp);
            _flows.Accept(goingLeft);

            Assert.Equal(3, _flows.Active.Count);

            _motion.Tick(1d, _flows);

            Assert.Equal(3, _flows.Active.Count);
            Assert.Equal(1.5d, goingRight.Position.X, 3);
            Assert.Equal(0.5d, goingRight.Position.Y, 3);
            Assert.Equal(3.5d, goingUp.Position.X, 3);
            // Y 轴向下，「向上」是 Y 减小
            Assert.Equal(1.5d, goingUp.Position.Y, 3);
            Assert.Equal(5.5d, goingLeft.Position.X, 3);
            Assert.Equal(4.5d, goingLeft.Position.Y, 3);
        }

        [Fact]
        public void SameGrid_OneEntityAbsorbed_OthersKeepRunning()
        {
            // 一格一个装置只吸收撞上它的那个流源，同网格里的其他流源不受牵连
            CollectorDevice collector = new CollectorDevice();
            _grid.SetRouter(1, 0, collector);

            FlowEntity absorbed = new FlowEntity(
                new FlowPos(0.5d, 0.5d), new FlowMotionData(1, 1), TestFlow.Make(10d, 20d));
            FlowEntity untouched = new FlowEntity(
                new FlowPos(0.5d, 5.5d), new FlowMotionData(1, 1), TestFlow.Make(10d, 20d));

            _flows.Accept(absorbed);
            _flows.Accept(untouched);

            _motion.Tick(1d, _flows);

            Assert.Equal(10d, collector.Stock.Get(FlowProps.Water));
            Assert.DoesNotContain(absorbed, _flows.Active);

            Assert.Contains(untouched, _flows.Active);
            Assert.Equal(1.5d, untouched.Position.X, 3);
        }

        [Fact]
        public void DeviceAtGridEdge_StopsInsideCell_NotKilledByBounds()
        {
            // 装置贴着网格左边界（x=0）时，向左撞停曾经算出 -0.001，
            // 下一帧 Floor 变成 -1 直接被越界回收。撞停必须落在格内。
            CollectorDevice collector = new CollectorDevice();
            _grid.SetRouter(0, 0, collector)
                .SetBounds(0, 0, 4, 4);

            FlowEntity entity = new FlowEntity(
                new FlowPos(2.5d, 0.5d), new FlowMotionData(3, 1), TestFlow.Make(10d, 20d));

            _flows.Accept(entity);

            _motion.Tick(3d, _flows);   // 足够撞上 x=0 的装置

            Assert.Equal(10d, collector.Stock.Get(FlowProps.Water));
        }

        [Fact]
        public void DirectionTable_UpMeansSmallerY()
        {
            // 游戏网格 Y 轴向下（anchoredPosition = (x, -y)），
            // 所以「上」必须是 Y 减小；写成 +1 会让流源在界面上朝反方向跑。
            Assert.Equal(-1, FlowMotionData.StepY(0));
            Assert.Equal(0, FlowMotionData.StepY(1));
            Assert.Equal(1, FlowMotionData.StepY(2));
            Assert.Equal(0, FlowMotionData.StepY(3));

            // 四个方向两两互反，且旋转仍按 +1 顺时针
            for (int direction = 0; direction < 4; direction++)
            {
                Assert.Equal(-FlowMotionData.StepX(direction), FlowMotionData.StepX((direction + 2) % 4));
                Assert.Equal(-FlowMotionData.StepY(direction), FlowMotionData.StepY((direction + 2) % 4));
            }
        }

        [Fact]
        public void BlockedEntity_StopsAtDeviceCellCenter()
        {
            CollectorDevice collector = new CollectorDevice();
            _grid.SetRouter(3, 2, collector);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 2.3d), new FlowMotionData(1, 1), TestFlow.Make(10d, 20d));

            _flows.Accept(entity);
            _motion.Tick(3d, _flows);

            Assert.Equal(10d, collector.Stock.Get(FlowProps.Water));
            Assert.Equal(3d, entity.Position.X, 3);
            Assert.Equal(2d, entity.Position.Y, 3);
        }

        [Theory]
        [InlineData(0)]   // 上
        [InlineData(1)]   // 右
        [InlineData(2)]   // 下
        [InlineData(3)]   // 左
        public void Split_ChildAlwaysBornInsideDeviceCell(int exitDirection)
        {
            // 出口必须发 2 道，才会走「分裂」而不是「原地转向」。
            // 入口通道 = 流源运动方向的反方向：出口朝左时从右往左走，其余从左往右走。
            int moveDirection = exitDirection == 3 ? 3 : 1;
            int entryChannel = (moveDirection + 2) % 4;

            SplitDevice split = new AnySplit(
                new int[] { entryChannel, exitDirection },
                new int[] { 0, 2 });

            _grid.SetRouter(3, 3, split);

            double startX = exitDirection == 3 ? 5d : 1d;
            FlowEntity entity = new FlowEntity(
                new FlowPos(startX, 3d),
                new FlowMotionData(moveDirection, 1),
                TestFlow.Make(10d, 20d));

            _flows.Accept(entity);
            _motion.Tick(3d, _flows);

            Assert.Equal(2, _flows.Active.Count);

            // 出生格必须就是装置格。朝上/朝左时若跟着方向做负偏移，就会落到隔壁去。
            foreach (FlowEntity child in _flows.Active)
            {
                Assert.Equal(3, (int)Math.Floor(child.Position.X));
                Assert.Equal(3, (int)Math.Floor(child.Position.Y));
                Assert.Equal(3d, child.Position.X);
                Assert.Equal(3d, child.Position.Y);
                Assert.Equal(exitDirection, child.Motion.Direction);
            }
        }

        [Fact]
        public void Split_ChildrenBornOnDeviceCell_NoExponentialGrowth()
        {
            // 流源向右(1)撞上 (3,3) 的分流器，入口通道是反方向的左(3)，出口是下(2)发 2 道。
            SplitDevice split = new AnySplit(
                new int[] { 3, 2 }, new int[] { 0, 2 });
            _grid.SetRouter(3, 3, split);

            FlowEntity entity = new FlowEntity(
                new FlowPos(1.5d, 3.5d), new FlowMotionData(1, 1), TestFlow.Make(100d, 20d));

            _flows.Accept(entity);
            _motion.Tick(3d, _flows);      // 足够从 x=1.5 走进装置格

            Assert.Equal(2, _flows.Active.Count);

            // 子流源必须出生在装置格上。若出生在撞停前的上一格，它们下一帧会再撞同一个
            // 装置，每帧翻倍——这条断言就是钉住那个回归的。
            foreach (FlowEntity child in _flows.Active)
            {
                Assert.Equal(3, (int)Math.Floor(child.Position.X));
                Assert.Equal(3, (int)Math.Floor(child.Position.Y));
                Assert.Equal(2, child.Motion.Direction);

                // 100 分 2 份：每份水 50，温度按 Keep 保留 20
                Assert.Equal(50d, child.Properties.Get(FlowProps.Water));
                Assert.Equal(20d, child.Properties.Get(FlowProps.Temperature));
            }

            // 再跑若干帧，数量不能增长
            for (int i = 0; i < 20; i++)
            {
                _motion.Tick(0.5d, _flows);
            }

            Assert.Equal(2, _flows.Active.Count);
        }

        [Fact]
        public void Split_NeverTakesControl_LeavesPositionUntouched()
        {
            // 装置放在 (3,4)，通道只有左右(1/3)；流源向上(0)运动，
            // 入口通道需要是下(2)，装置不收 → Route 返回 false。
            SplitDevice split = new AnySplit(new int[] { 1, 3 });
            _grid.SetRouter(3, 4, split);

            FlowEntity entity = new FlowEntity(
                new FlowPos(3.5d, 5.5d), new FlowMotionData(0, 1), TestFlow.Make(100d, 20d));

            _flows.Accept(entity);
            _motion.Tick(1d, _flows);      // 一帧走一格，正好穿过装置格

            // 位置必须按正常移动走到 4.5：既不能被截停在格边缘，也不能被回滚丢步
            Assert.Single(_flows.Active);
            Assert.Equal(3.5d, entity.Position.X, 3);
            Assert.Equal(4.5d, entity.Position.Y, 3);
        }

        [Fact]
        public void KillAll_BroadcastsEachEntityExactlyOnce()
        {
            List<FlowEntity> removed = new List<FlowEntity>();
            _flows.OnEntityRemoved += removed.Add;

            FlowEntity a = new FlowEntity(new FlowPos(0, 0), new FlowMotionData(1, 1), TestFlow.Make(1, 1));
            FlowEntity b = new FlowEntity(new FlowPos(0, 0), new FlowMotionData(1, 1), TestFlow.Make(1, 1));

            _flows.Accept(a);
            _flows.Accept(b);
            _flows.KillAll();

            Assert.Equal(2, removed.Count);
            Assert.Empty(_flows.Active);

            // 已经清空后再来一次，不应重复播报
            _flows.KillAll();
            Assert.Equal(2, removed.Count);
        }
    }
}
