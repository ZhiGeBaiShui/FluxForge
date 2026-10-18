using System.Collections.Generic;
using Xunit;

namespace ProbablyBagMod.Tests
{
    /// <summary>
    /// 发射 / 收集 / 冰块三种装置的行为验证。
    ///
    /// 这几个装置都不碰游戏类型，可以直接跑——
    /// 「装置逻辑对不对」和「游戏里显示得对不对」是两件事，先在这里把前者钉死。
    /// </summary>
    public class DeviceTests
    {
        public DeviceTests()
        {
            FlowSpawner.Instance.Reset();
            FlowManager.Instance.Reset();
        }

        private readonly FakeGrid _grid = new FakeGrid();

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

        private static List<FlowEntity> CaptureSpawned()
        {
            List<FlowEntity> spawned = new List<FlowEntity>();
            FlowSpawner.Instance.OnEntityAdded += spawned.Add;
            return spawned;
        }

        [Fact]
        public void Emitter_EmitsWholeStockAsOneEntity()
        {
            List<FlowEntity> spawned = CaptureSpawned();
            EmitterDevice emitter = new EmitterDevice(100d, 30d);

            bool emitted = emitter.EmitAll(new FlowPos(3.5d, 3.5d), 2, _grid);
            FlowSpawner.Instance.Advance(0d);

            Assert.True(emitted);
            Assert.Single(spawned);                       // 整份存量 → 一个流源
            Assert.Equal(100d, spawned[0].Properties.Moisture);
            Assert.Equal(30d, spawned[0].Properties.Temperature);
            Assert.Equal(2, spawned[0].Motion.Direction);
            Assert.Equal(0d, emitter.Stock.Moisture);     // 发完清空
        }

        [Fact]
        public void Emitter_SecondEmitWithoutRefill_DoesNothing()
        {
            List<FlowEntity> spawned = CaptureSpawned();
            EmitterDevice emitter = new EmitterDevice(50d, 20d);

            Assert.True(emitter.EmitAll(new FlowPos(0, 0), 0, _grid));
            Assert.False(emitter.EmitAll(new FlowPos(0, 0), 0, _grid));
            FlowSpawner.Instance.Advance(0d);

            Assert.Single(spawned);
        }

        [Fact]
        public void Emitter_EmptyStock_DoesNotEmit()
        {
            List<FlowEntity> spawned = CaptureSpawned();
            EmitterDevice emitter = new EmitterDevice(0d, 20d);

            Assert.False(emitter.EmitAll(new FlowPos(0, 0), 0, _grid));
            FlowSpawner.Instance.Advance(0d);

            Assert.Empty(spawned);
        }

        [Fact]
        public void Emitter_NoGrid_DoesNotEmit()
        {
            List<FlowEntity> spawned = CaptureSpawned();
            EmitterDevice emitter = new EmitterDevice(100d, 20d);

            Assert.False(emitter.EmitAll(new FlowPos(0, 0), 0, null));
            FlowSpawner.Instance.Advance(0d);

            Assert.Empty(spawned);
            Assert.Equal(100d, emitter.Stock.Moisture);
        }

        [Fact]
        public void Collector_AccumulatesAndKillsIncoming()
        {
            CollectorDevice collector = new CollectorDevice();

            collector.Route(new FlowEntity(new FlowPos(0, 0), new FlowMotionData(0, 1), new FlowProperties(40, 10), _grid));
            collector.Route(new FlowEntity(new FlowPos(0, 0), new FlowMotionData(0, 1), new FlowProperties(60, 20), _grid));

            Assert.Equal(100d, collector.Stock.Moisture);
            Assert.Equal(2, collector.Collected.Count);
            // 加权平均：(40×10 + 60×20) / 100 = 16
            Assert.Equal(16d, collector.Stock.Temperature, 3);

            collector.Clear();
            Assert.Equal(0d, collector.Stock.Moisture);
            Assert.Empty(collector.Collected);
        }

        [Fact]
        public void Collector_RouteReportsChanged()
        {
            CollectorDevice collector = new CollectorDevice();
            int changedCount = 0;
            double moistureAtEvent = -1d;

            collector.Changed += sender =>
            {
                changedCount++;
                moistureAtEvent = ((CollectorDevice)sender).Stock.Moisture;
            };

            collector.Route(new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                new FlowProperties(40d, 10d),
                _grid));

            Assert.Equal(1, changedCount);
            // 播报时库存已经合并完成，订阅方读到的是一次完整的吸收结果
            Assert.Equal(40d, moistureAtEvent);
        }

        [Fact]
        public void TwoGrids_AdvanceIndependently()
        {
            // 网格 A 在 (1,0) 放了收集器，网格 B 同样位置是空的：
            // 两个流源同帧推进，A 的流源被吸走，B 的必须原样走完这一帧
            CollectorDevice collector = new CollectorDevice();
            FakeGrid gridA = new FakeGrid().SetRouter(1, 0, collector);
            FakeGrid gridB = new FakeGrid();

            FlowEntity entityA = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                new FlowProperties(10d, 20d),
                gridA);
            FlowEntity entityB = new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, 1),
                new FlowProperties(10d, 20d),
                gridB);

            FlowSpawner.Instance.Register(entityA, 0d);
            FlowSpawner.Instance.Register(entityB, 0d);
            FlowSpawner.Instance.Advance(0d);

            FlowManager.Instance.Tick(1d);

            Assert.Single(collector.Collected);
            Assert.Same(entityA, collector.Collected[0]);
            Assert.DoesNotContain(entityA, FlowManager.Instance.ActiveEntities);

            Assert.Contains(entityB, FlowManager.Instance.ActiveEntities);
            Assert.Equal(1.5d, entityB.Position.X, 3);
            Assert.Equal(0.5d, entityB.Position.Y, 3);
        }

        [Fact]
        public void IceBlock_TransfersMoistureAndMixesTemperature()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            FlowProperties flow = new FlowProperties(100d, 25d);

            bool reacted = ice.TryReact(flow);

            Assert.True(reacted);
            // 流源能带走自身质量的一半（50），远大于冰块剩余的 10，所以一次化完
            Assert.Equal(0d, ice.Moisture);
            Assert.Equal(110d, flow.Moisture);
            // (100×25 + 10×(-5)) / 110 ≈ 22.273
            Assert.Equal(22.273d, flow.Temperature, 3);
        }

        [Fact]
        public void IceBlock_SmallFlowOnlyMeltsHalfItsOwnMass()
        {
            // 一杯水化不完整块冰：流源只带 100，最多带走 50，冰还剩 950
            IceBlockDevice ice = new IceBlockDevice(1000d, -10d);
            FlowProperties flow = new FlowProperties(100d, 20d);

            bool exhausted = false;
            ice.Exhausted += sender => exhausted = true;

            Assert.True(ice.TryReact(flow));

            Assert.Equal(950d, ice.Moisture);
            Assert.Equal(150d, flow.Moisture);
            // (100×20 + 50×(-10)) / 150 = 10
            Assert.Equal(10d, flow.Temperature, 3);
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
            while (ice.Moisture > 0d && rounds < 100)
            {
                ice.TryReact(new FlowProperties(100d, 20d));
                rounds++;
            }

            Assert.Equal(0d, ice.Moisture);
            Assert.Equal(1, exhaustedCount);   // 只在真正归零的那一次触发
            Assert.True(rounds < 100);         // 不会无限循环
        }

        [Fact]
        public void IceBlock_ReportsChangedThenExhausted_StockAlreadyZero()
        {
            IceBlockDevice ice = new IceBlockDevice(10d, -5d);
            List<string> order = new List<string>();
            double moistureWhenChanged = -1d;
            double moistureWhenExhausted = -1d;

            ice.Changed += sender =>
            {
                order.Add("Changed");
                // Changed 必须已经能读到最终值：订阅方就是靠它把 tag 写成 0 的。
                // 如果哪天把播报挪到 Moisture = 0 之前，这条断言会立刻失败。
                moistureWhenChanged = ((IceBlockDevice)sender).Moisture;
            };
            ice.Exhausted += sender =>
            {
                order.Add("Exhausted");
                moistureWhenExhausted = ((IceBlockDevice)sender).Moisture;
            };

            Assert.True(ice.TryReact(new FlowProperties(100d, 25d)));

            // 顺序不可调换：Exhausted 的订阅方会把物品从网格摘掉，
            // Changed 必须先把「存量归零」同步出去
            Assert.Equal(new[] { "Changed", "Exhausted" }, order);
            Assert.Equal(0d, moistureWhenChanged);
            Assert.Equal(0d, moistureWhenExhausted);
        }

        [Fact]
        public void IceBlock_ReturnsFalseWhenMelted()
        {
            IceBlockDevice ice = new IceBlockDevice(0d, -5d);
            FlowProperties flow = new FlowProperties(100d, 25d);

            Assert.False(ice.TryReact(flow));
            Assert.Equal(100d, flow.Moisture);   // 没反应就不该动流源
            Assert.Equal(25d, flow.Temperature);
        }

        [Fact]
        public void SameGrid_HoldsMultipleEntities_EachAdvancesIndependently()
        {
            // 一块网格里同时跑三个流源：位置不同、方向不同，谁都不该被谁挤掉。
            // 「流源不是一格一个」是这套框架的前提，发射装置一秒钟能吐好几个。
            FakeGrid grid = new FakeGrid();

            FlowEntity goingRight = new FlowEntity(
                new FlowPos(0.5d, 0.5d), new FlowMotionData(1, 1), new FlowProperties(10d, 20d), grid);
            FlowEntity goingUp = new FlowEntity(
                new FlowPos(3.5d, 2.5d), new FlowMotionData(0, 1), new FlowProperties(11d, 21d), grid);
            FlowEntity goingLeft = new FlowEntity(
                new FlowPos(6.5d, 4.5d), new FlowMotionData(3, 1), new FlowProperties(12d, 22d), grid);

            FlowSpawner.Instance.Register(goingRight, 0d);
            FlowSpawner.Instance.Register(goingUp, 0d);
            FlowSpawner.Instance.Register(goingLeft, 0d);
            FlowSpawner.Instance.Advance(0d);

            Assert.Equal(3, FlowManager.Instance.ActiveEntities.Count);

            FlowManager.Instance.Tick(1d);

            Assert.Equal(3, FlowManager.Instance.ActiveEntities.Count);
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
            FakeGrid grid = new FakeGrid().SetRouter(1, 0, collector);

            FlowEntity absorbed = new FlowEntity(
                new FlowPos(0.5d, 0.5d), new FlowMotionData(1, 1), new FlowProperties(10d, 20d), grid);
            FlowEntity untouched = new FlowEntity(
                new FlowPos(0.5d, 5.5d), new FlowMotionData(1, 1), new FlowProperties(10d, 20d), grid);

            FlowSpawner.Instance.Register(absorbed, 0d);
            FlowSpawner.Instance.Register(untouched, 0d);
            FlowSpawner.Instance.Advance(0d);

            FlowManager.Instance.Tick(1d);

            Assert.Single(collector.Collected);
            Assert.Same(absorbed, collector.Collected[0]);
            Assert.DoesNotContain(absorbed, FlowManager.Instance.ActiveEntities);

            Assert.Contains(untouched, FlowManager.Instance.ActiveEntities);
            Assert.Equal(1.5d, untouched.Position.X, 3);
        }

        [Fact]
        public void DeviceAtGridEdge_StopsInsideCell_NotKilledByBounds()
        {
            // 装置贴着网格左边界（x=0）时，向左撞停曾经算出 -0.001，
            // 下一帧 Floor 变成 -1 直接被越界回收。撞停必须落在格内。
            CollectorDevice collector = new CollectorDevice();
            FakeGrid grid = new FakeGrid().SetRouter(0, 0, collector)
                .SetBounds(0, 0, 4, 4);

            FlowEntity entity = new FlowEntity(
                new FlowPos(2.5d, 0.5d), new FlowMotionData(3, 1), new FlowProperties(10d, 20d), grid);

            FlowSpawner.Instance.Register(entity, 0d);
            FlowSpawner.Instance.Advance(0d);

            FlowManager.Instance.Tick(3d);   // 足够撞上 x=0 的装置

            Assert.Single(collector.Collected);
            Assert.Same(entity, collector.Collected[0]);
        }

        [Fact]
        public void DirectionTable_UpMeansSmallerY()
        {
            // 游戏网格 Y 轴向下（anchoredPosition = (x, -y)），
            // 所以「上」必须是 Y 减小；写成 +1 会让流源在界面上朝反方向跑。
            Assert.Equal(-1, FlowMotionData.Dy[0]);
            Assert.Equal(0, FlowMotionData.Dy[1]);
            Assert.Equal(1, FlowMotionData.Dy[2]);
            Assert.Equal(0, FlowMotionData.Dy[3]);

            // 四个方向两两互反，且旋转仍按 +1 顺时针
            for (int direction = 0; direction < 4; direction++)
            {
                Assert.Equal(-FlowMotionData.Dx[direction], FlowMotionData.Dx[(direction + 2) % 4]);
                Assert.Equal(-FlowMotionData.Dy[direction], FlowMotionData.Dy[(direction + 2) % 4]);
            }
        }

        [Fact]
        public void Collector_KeepsBoundedHistory()
        {
            // 发射装置一秒吐两个，不封顶的话一天是几十万个对象
            CollectorDevice collector = new CollectorDevice();
            FakeGrid grid = new FakeGrid();

            int total = CollectorDevice.MaxCollected + 20;
            for (int i = 0; i < total; i++)
            {
                FlowEntity entity = new FlowEntity(
                    new FlowPos(0.5d, 0.5d), new FlowMotionData(1, 1), new FlowProperties(1d, 20d), grid);
                collector.Route(entity);
            }

            Assert.Equal(CollectorDevice.MaxCollected, collector.Collected.Count);
            // 存量照旧全量累积，不受记录上限影响
            Assert.Equal(total, collector.Stock.Moisture, 3);
        }

        [Fact]
        public void BlockedEntity_KeepsPerpendicularAxisUnchanged()
        {
            // 流源沿 X 轴撞上装置时，Y 不该被瞬移到格中线（那会看起来像跳了一下）
            CollectorDevice collector = new CollectorDevice();
            FakeGrid grid = new FakeGrid().SetRouter(3, 2, collector);

            FlowEntity entity = new FlowEntity(
                new FlowPos(0.5d, 2.3d), new FlowMotionData(1, 1), new FlowProperties(10d, 20d), grid);

            FlowSpawner.Instance.Register(entity, 0d);
            FlowSpawner.Instance.Advance(0d);
            FlowManager.Instance.Tick(3d);

            Assert.Single(collector.Collected);
            Assert.Equal(2.3d, entity.Position.Y, 3);
        }
    }
}
