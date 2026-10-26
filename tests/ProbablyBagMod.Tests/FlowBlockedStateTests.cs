using System;
using System.Collections.Generic;
using Xunit;

namespace ProbablyBagMod.Tests
{
    public class FlowBlockedStateTests
    {
        private const double StillDeltaTime = 1e-20d;
        private readonly FakeGrid _grid = new FakeGrid();
        private readonly FlowSpawner _flows = new FlowSpawner();
        private readonly FlowManager _motion;

        public FlowBlockedStateTests()
        {
            _motion = new FlowManager(_grid, _flows);
        }

        private sealed class FakeGrid : IFlowGrid
        {
            private readonly Dictionary<(int, int), IFlowRouter> _routers
                = new Dictionary<(int, int), IFlowRouter>();

            public bool InBounds { get; set; } = true;

            public void SetRouter(int x, int y, IFlowRouter router)
            {
                _routers[(x, y)] = router;
            }

            public bool Contains(int cellX, int cellY)
            {
                return InBounds;
            }

            public IFlowRouter GetRouterAt(int cellX, int cellY)
            {
                return _routers.TryGetValue((cellX, cellY), out IFlowRouter router)
                    ? router : null;
            }

            public IFlowDevice GetReactorAt(int cellX, int cellY)
            {
                return null;
            }
        }

        private static FlowEntity CreateEntity(double speed = 1d)
        {
            return new FlowEntity(
                new FlowPos(0.5d, 0.5d),
                new FlowMotionData(1, speed),
                new FlowProperties(10d, 20d));
        }

        private List<(FlowEntity Entity, bool Blocked)> CaptureChanges()
        {
            List<(FlowEntity Entity, bool Blocked)> changes
                = new List<(FlowEntity Entity, bool Blocked)>();
            _flows.OnEntityBlockedChanged += (entity, blocked) =>
                changes.Add((entity, blocked));
            return changes;
        }

        private void TickStill(params FlowEntity[] entities)
        {
            List<(double X, double Y)> positions = new List<(double X, double Y)>();
            foreach (FlowEntity entity in entities)
            {
                positions.Add((entity.Position.X, entity.Position.Y));
            }

            // 正时间步照常运动，但位移小于 double 精度；不能用近似比较代替原规则。
            _motion.Tick(StillDeltaTime, _flows);

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.Equal(positions[i].X, entities[i].Position.X);
                Assert.Equal(positions[i].Y, entities[i].Position.Y);
            }
        }

        [Fact]
        public void Accept_BroadcastsBirth_WithoutFalseEvent()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            int added = 0;
            _flows.OnEntityAdded += current =>
            {
                added++;
                Assert.Same(entity, current);
                Assert.Contains(current, _flows.Active);
            };

            _flows.Accept(entity);

            Assert.Equal(1, added);
            Assert.Empty(changes);
        }

        [Fact]
        public void StillSamples_BlockOnSecond_NotifyOnlyChanges()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                Assert.Same(entity, current);
                Assert.True(blocked);
            };
            _flows.Accept(entity);

            TickStill(entity);
            Assert.Equal(0.5d, entity.Position.X);
            Assert.Equal(0.5d, entity.Position.Y);
            Assert.Empty(changes);

            TickStill(entity);
            Assert.Equal((entity, true), Assert.Single(changes));

            TickStill(entity);
            TickStill(entity);
            Assert.Single(changes);
        }

        [Theory]
        [InlineData(0d, false)]
        [InlineData(0.01d, false)]
        [InlineData(0.010000000000000002d, true)]
        [InlineData(1d, true)]
        public void SpeedThreshold_IsStrictlyGreaterThanPointZeroOne(double speed, bool blocked)
        {
            FlowEntity entity = CreateEntity(speed);
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);

            TickStill(entity);
            TickStill(entity);

            Assert.Equal(blocked ? 1 : 0, changes.Count);
            if (blocked)
            {
                Assert.Equal((entity, true), Assert.Single(changes));
            }
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(false, true)]
        public void ExactAxisChange_ResetsHistory_AndRestartsTwoSamples(bool changeX, bool initiallyBlocked)
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            if (initiallyBlocked)
            {
                TickStill(entity);
            }

            double changed = Math.BitIncrement(0.5d);
            Assert.NotEqual(0.5d, changed);
            entity.Position.Set(changeX ? changed : 0.5d, changeX ? 0.5d : changed);
            TickStill(entity);

            Assert.Equal(initiallyBlocked ? 2 : 0, changes.Count);
            if (initiallyBlocked)
            {
                Assert.Equal((entity, false), changes[1]);
            }

            TickStill(entity);
            Assert.Equal(initiallyBlocked ? 2 : 0, changes.Count);
            TickStill(entity);
            Assert.Equal((entity, true), changes[changes.Count - 1]);
            Assert.Equal(initiallyBlocked ? 3 : 1, changes.Count);
        }

        [Fact]
        public void NormalMovement_RecoversOnce_UsingFinalPosition()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                Assert.False(blocked);
                Assert.Equal(0.75d, current.Position.X);
            };

            _motion.Tick(0.25d, _flows);

            Assert.Equal(0.75d, entity.Position.X);
            Assert.Equal(0.5d, entity.Position.Y);
            Assert.Equal(new[] { (entity, true), (entity, false) }, changes);

            _motion.Tick(0.25d, _flows);
            Assert.Equal(1d, entity.Position.X);
            Assert.Equal(2, changes.Count);
        }

        [Theory]
        [InlineData(0d)]
        [InlineData(0.01d)]
        public void LowerSpeed_RecoversOnce_ThenNeedsTwoNewSamples(double speed)
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);

            entity.Motion.Speed = speed;
            TickStill(entity);
            TickStill(entity);
            Assert.Equal(new[] { (entity, true), (entity, false) }, changes);

            entity.Motion.Speed = 1d;
            TickStill(entity);
            Assert.Equal(2, changes.Count);
            TickStill(entity);
            Assert.Equal(3, changes.Count);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        public void ZeroTick_FreezesCountStateAndLastValidSample(int samples)
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            for (int i = 0; i < samples; i++)
            {
                TickStill(entity);
            }

            entity.Position.X = 0.75d;
            _motion.Tick(0d, _flows);
            _motion.Tick(0d, _flows);

            Assert.Equal(samples == 2 ? 1 : 0, changes.Count);

            TickStill(entity);
            Assert.Equal(samples == 2 ? 2 : 0, changes.Count);
            TickStill(entity);
            Assert.Equal(samples == 2 ? 2 : 0, changes.Count);
            TickStill(entity);
            Assert.Equal((entity, true), changes[changes.Count - 1]);
        }

        [Fact]
        public void Kill_DropsHistory_NoFalse_AndReacceptStartsAtNewPosition()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);
            int removed = 0;
            _flows.OnEntityRemoved += current =>
            {
                removed++;
                Assert.DoesNotContain(current, _flows.Active);
            };

            _flows.Kill(entity);
            _flows.Kill(entity);
            Assert.Equal(1, removed);
            Assert.Equal((entity, true), Assert.Single(changes));

            entity.Position.Set(0.75d, 0.875d);
            _flows.Accept(entity);
            Assert.Single(changes);
            TickStill(entity);
            Assert.Single(changes);
            TickStill(entity);
            Assert.Equal(new[] { (entity, true), (entity, true) }, changes);
        }

        [Fact]
        public void KillAll_ClearsBlockedAndPendingEntities_WithoutFalseOrDuplicateRemoval()
        {
            FlowEntity blocked = CreateEntity();
            FlowEntity pending = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(blocked);
            TickStill(blocked);
            TickStill(blocked);
            _flows.Accept(pending);
            TickStill(blocked, pending);
            List<FlowEntity> removed = new List<FlowEntity>();
            _flows.OnEntityRemoved += current =>
            {
                Assert.DoesNotContain(current, _flows.Active);
                removed.Add(current);
            };

            _flows.KillAll();
            _flows.KillAll();

            Assert.Empty(_flows.Active);
            Assert.Equal(new[] { pending, blocked }, removed);
            Assert.Equal((blocked, true), Assert.Single(changes));
            _flows.Accept(blocked);
            _flows.Accept(pending);
            TickStill(blocked, pending);
            Assert.Single(changes);
            TickStill(blocked, pending);
            Assert.Equal(3, changes.Count);
        }

        [Fact]
        public void OutOfBoundsDeath_IsNotSampledAgain()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);
            _grid.InBounds = false;

            _motion.Tick(StillDeltaTime, _flows);

            Assert.Empty(_flows.Active);
            Assert.Equal((entity, true), Assert.Single(changes));
        }

        [Fact]
        public void CollectorDeathDuringMotion_IsNotSampledAfterStopAt()
        {
            CollectorDevice collector = new CollectorDevice();
            _grid.SetRouter(1, 0, collector);
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);

            _motion.Tick(1d, _flows);

            Assert.Same(entity, Assert.Single(collector.Collected));
            Assert.Empty(_flows.Active);
            Assert.Equal(1.001d, entity.Position.X);
            Assert.Equal((entity, true), Assert.Single(changes));
        }

        [Fact]
        public void SplitBirthsDuringMotion_GetFirstSampleImmediately()
        {
            SplitDevice split = new SplitDevice(0, new[] { 3, 2 }, new[] { 0, 2 });
            _grid.SetRouter(3, 3, split);
            FlowEntity parent = CreateEntity();
            parent.Position.Set(1.5d, 3.5d);
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(parent);
            TickStill(parent);
            TickStill(parent);
            List<FlowEntity> born = new List<FlowEntity>();
            _flows.OnEntityAdded += current =>
            {
                born.Add(current);
            };

            _motion.Tick(3d, _flows);

            Assert.Equal(2, born.Count);
            Assert.Equal(2, _flows.Active.Count);
            Assert.DoesNotContain(parent, _flows.Active);
            Assert.Equal((parent, true), Assert.Single(changes));
            foreach (FlowEntity child in born)
            {
                Assert.Contains(child, _flows.Active);
                Assert.Equal(3.5d, child.Position.X);
                Assert.Equal(3.5d, child.Position.Y);
                Assert.Equal(2, child.Motion.Direction);
                Assert.Equal(1d, child.Motion.Speed);
            }

            TickStill(born.ToArray());
            Assert.Equal(new[] { (parent, true), (born[1], true), (born[0], true) }, changes);
        }

        [Fact]
        public void BlockedCallback_AcceptedEntityWaitsUntilNextSamplingPass()
        {
            FlowEntity parent = CreateEntity();
            FlowEntity child = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(parent);
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                if (ReferenceEquals(current, parent) && blocked)
                {
                    _flows.Accept(child);
                }
            };
            TickStill(parent);

            TickStill(parent);

            Assert.Equal(2, _flows.Active.Count);
            Assert.Equal((parent, true), Assert.Single(changes));

            TickStill(parent, child);
            Assert.Single(changes);
            TickStill(parent, child);
            Assert.Equal(new[] { (parent, true), (child, true) }, changes);
        }

        [Fact]
        public void ReverseSampling_CurrentEntityKill_DoesNotSkipOtherLiveEntityOrWriteBack()
        {
            FlowEntity first = CreateEntity();
            FlowEntity last = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(first);
            _flows.Accept(last);
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                if (ReferenceEquals(current, last))
                {
                    Assert.True(blocked);
                    _flows.Kill(current);
                }
            };
            TickStill(first, last);

            TickStill(first, last);

            Assert.Same(first, Assert.Single(_flows.Active));
            Assert.Equal(new[] { (last, true), (first, true) }, changes);
        }

        [Fact]
        public void EarlierSubscriberKill_LaterSubscriberReceivesOriginalBoolSnapshot()
        {
            FlowEntity entity = CreateEntity();
            List<bool> snapshots = new List<bool>();
            Dictionary<FlowEntity, bool> cache = new Dictionary<FlowEntity, bool>();
            _flows.OnEntityAdded += current => cache.Add(current, false);
            _flows.OnEntityRemoved += current => cache.Remove(current);
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                Assert.True(blocked);
                _flows.Kill(current);
            };
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                snapshots.Add(blocked);
                Assert.DoesNotContain(current, _flows.Active);
                if (cache.ContainsKey(current))
                {
                    cache[current] = blocked;
                }
            };
            _flows.Accept(entity);
            TickStill(entity);

            TickStill(entity);

            Assert.Equal(new[] { true }, snapshots);
            Assert.Empty(cache);
            Assert.Empty(_flows.Active);
        }

        [Fact]
        public void EventException_PropagatesAfterCommit_AndSameChangeIsNotReplayed()
        {
            FlowEntity entity = CreateEntity();
            InvalidOperationException failure = new InvalidOperationException("subscriber failed");
            int attempts = 0;
            _flows.OnEntityBlockedChanged += (current, blocked) =>
            {
                attempts++;
                Assert.True(blocked);
                throw failure;
            };
            _flows.Accept(entity);
            TickStill(entity);

            Assert.Same(failure, Assert.Throws<InvalidOperationException>(
                () => _motion.Tick(StillDeltaTime, _flows)));

            TickStill(entity);
            Assert.Equal(1, attempts);
        }

        [Fact]
        public void ReportBlockedState_OnlyForwardsPayload()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.ReportBlockedState(entity, true);
            _flows.ReportBlockedState(entity, true);
            _flows.ReportBlockedState(entity, false);
            Assert.Equal(new[] { (entity, true), (entity, true), (entity, false) }, changes);
        }

        [Fact]
        public void TwoMachines_HaveIndependentSamplingEventsAndCleanup()
        {
            FlowSpawner otherFlows = new FlowSpawner();
            FlowManager otherMotion = new FlowManager(new FakeGrid(), otherFlows);
            FlowEntity first = CreateEntity();
            FlowEntity other = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            List<bool> otherChanges = new List<bool>();
            otherFlows.OnEntityBlockedChanged += (current, blocked) =>
            {
                Assert.Same(other, current);
                otherChanges.Add(blocked);
            };
            _flows.Accept(first);
            otherFlows.Accept(other);

            TickStill(first);
            TickStill(first);
            Assert.Equal((first, true), Assert.Single(changes));
            Assert.Empty(otherChanges);

            otherMotion.Tick(StillDeltaTime, otherFlows);
            Assert.Equal(0.5d, other.Position.X);
            Assert.Equal(0.5d, other.Position.Y);
            Assert.Empty(otherChanges);
            _flows.KillAll();
            otherMotion.Tick(StillDeltaTime, otherFlows);
            Assert.Equal(new[] { true }, otherChanges);
            Assert.Equal((first, true), Assert.Single(changes));
            Assert.Same(other, Assert.Single(otherFlows.Active));
        }

        [Fact]
        public void SharedMotion_StaggeredAcceptAndPositionChange_KeepEntityHistoryIndependent()
        {
            FlowEntity first = CreateEntity();
            FlowEntity second = CreateEntity();
            second.Motion = first.Motion;
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(first);
            TickStill(first);

            _flows.Accept(second);
            Assert.Same(first.Motion, second.Motion);
            TickStill(first, second);
            Assert.Equal((first, true), Assert.Single(changes));

            first.Position.X = Math.BitIncrement(0.5d);
            TickStill(first, second);
            Assert.Equal(new[] { (first, true), (second, true), (first, false) }, changes);
        }

        [Fact]
        public void BirthPosition_IsRememberedBeforeFirstTick()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            entity.Position.X = Math.BitIncrement(0.5d);

            TickStill(entity);
            TickStill(entity);
            Assert.Empty(changes);
            TickStill(entity);
            Assert.Equal((entity, true), Assert.Single(changes));
        }

        [Fact]
        public void OriginalConstructor_BindsExistingEntitiesBeforeMovement()
        {
            FlowSpawner flows = new FlowSpawner();
            FlowManager motion = new FlowManager(new FakeGrid());
            FlowEntity entity = CreateEntity();
            List<bool> changes = new List<bool>();
            flows.OnEntityBlockedChanged += (_, blocked) => changes.Add(blocked);
            flows.Accept(entity);

            motion.Tick(StillDeltaTime, flows);
            Assert.Empty(changes);
            motion.Tick(StillDeltaTime, flows);
            Assert.Equal(new[] { true }, changes);

            flows.Kill(entity);
            flows.Accept(entity);
            motion.Tick(StillDeltaTime, flows);
            Assert.Single(changes);
            motion.Tick(StillDeltaTime, flows);
            Assert.Equal(new[] { true, true }, changes);
        }

        [Fact]
        public void SwitchingSpawner_DetachesOldEventsAndResetsHistory()
        {
            FlowEntity entity = CreateEntity();
            List<(FlowEntity Entity, bool Blocked)> changes = CaptureChanges();
            _flows.Accept(entity);
            TickStill(entity);
            TickStill(entity);

            FlowSpawner otherFlows = new FlowSpawner();
            otherFlows.Accept(entity);
            List<bool> otherChanges = new List<bool>();
            otherFlows.OnEntityBlockedChanged += (_, blocked) => otherChanges.Add(blocked);
            _motion.Tick(StillDeltaTime, otherFlows);
            Assert.Empty(otherChanges);

            // 旧名单的死亡广播不应删除新绑定的记录。
            _flows.Kill(entity);
            _motion.Tick(StillDeltaTime, otherFlows);
            Assert.Equal(new[] { true }, otherChanges);

            _flows.Accept(entity);
            TickStill(entity);
            Assert.Single(changes);
            TickStill(entity);
            Assert.Equal(new[] { (entity, true), (entity, true) }, changes);
        }
    }
}
