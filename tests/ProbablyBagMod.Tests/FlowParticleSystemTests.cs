using System;
using System.Collections.Generic;
using ProbablyBagMod.Presentation.Flow.Particles;
using ProbablyBagMod.Presentation.Flow.Visual;
using Xunit;

namespace ProbablyBagMod.Tests
{
    public class FlowParticleSystemTests
    {
        [Fact]
        public void Update_DefaultRemainder_IntegratesOldParticlesBeforeSpawning()
        {
            Comparison comparison = new Comparison();
            FlowEntity entity = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot(3d, 4d);
            comparison.Add(entity);

            comparison.Update(entity, in snapshot, 0.0625f);
            comparison.CompleteFrame(0.0625f);
            Assert.Empty(comparison.Actual);

            comparison.Update(entity, in snapshot, 0.0625f);
            comparison.CompleteFrame(0.0625f);
            FlowAmbientParticle born = Assert.Single(comparison.Actual);
            Assert.Equal(born.MaxLife, born.Life);

            comparison.Update(entity, in snapshot, 0.125f);
            comparison.CompleteFrame(0.125f);
            Assert.Equal(2, comparison.Actual.Count);
            FlowAmbientParticle old = comparison.Actual[0];
            Assert.Equal(born.X + born.Vx * 0.125f, old.X);
            Assert.Equal(born.Y + born.Vy * 0.125f, old.Y);
            Assert.Equal(born.Life - 0.125f, old.Life);
            Assert.True(old.X != born.X || old.Y != born.Y);
            FlowAmbientParticle next = comparison.Actual[1];
            Assert.Equal(next.MaxLife, next.Life);
        }

        [Fact]
        public void CompleteFrame_MixedLifecycleSequence_MatchesOrderedBaseline()
        {
            Comparison comparison = new Comparison();
            FlowEntity first = MakeEntity();
            FlowEntity middle = MakeEntity();
            FlowEntity last = MakeEntity();
            FlowEntity replacement = MakeEntity();
            FlowFrameInput firstInput = MakeSnapshot(1d, 2d);
            FlowFrameInput middleInput = MakeSnapshot(20d, 30d, 0, FlowTemp.Cold, FlowContent.Small);
            FlowFrameInput lastInput = MakeSnapshot(-10d, -20d, 2, FlowTemp.Hot, FlowContent.Empty);
            comparison.Add(first);
            comparison.Add(middle);
            comparison.Add(last);

            comparison.Update(first, in firstInput, 0.25f);
            comparison.Update(middle, in middleInput, 0.25f);
            comparison.Update(last, in lastInput, 0.25f);
            comparison.CompleteFrame(0.25f);
            Assert.Equal(6, comparison.Actual.Count);

            firstInput = MakeSnapshot(1.25d, 2.5d, 3, FlowTemp.Hot, FlowContent.Empty,
                FlowState.Blocked, new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f));
            middleInput = MakeSnapshot(20.5d, 30.75d, 2, FlowTemp.Normal, FlowContent.Large,
                FlowState.Split);
            lastInput = MakeSnapshot(-9d, -19d, 0, FlowTemp.Cold, FlowContent.Mid,
                FlowState.Merge);
            comparison.Update(first, in firstInput, 0.1875f);
            comparison.Update(middle, in middleInput, 0.1875f);
            comparison.Update(last, in lastInput, 0.1875f);
            comparison.CompleteFrame(0.1875f);

            comparison.Remove(middle);
            comparison.Add(replacement);
            FlowFrameInput replacementInput = MakeSnapshot(100d, 200d);
            comparison.Update(first, in firstInput, 0.0625f);
            comparison.Update(last, in lastInput, 0.0625f);
            comparison.Update(replacement, in replacementInput, 0.125f);
            comparison.CompleteFrame(0.0625f);

            comparison.Remove(first);
            comparison.Remove(last);
            comparison.Update(replacement, in replacementInput, 0.125f);
            comparison.CompleteFrame(0.125f);
            comparison.ClearAmbient();
            comparison.CompleteFrame(0.0625f);
            Assert.Empty(comparison.Actual);
            comparison.Update(replacement, in replacementInput, 0.125f);
            comparison.CompleteFrame(0.125f);
            Assert.Single(comparison.Actual);
        }

        [Fact]
        public void Remove_StopsEmission_AndParticlesMoveUntilNaturallyEmpty()
        {
            Comparison comparison = new Comparison();
            FlowEntity first = MakeEntity();
            FlowEntity second = MakeEntity();
            FlowFrameInput firstInput = MakeSnapshot(4d, 5d);
            FlowFrameInput secondInput = MakeSnapshot(40d, 50d);
            comparison.Add(first);
            comparison.Add(second);
            comparison.Update(first, in firstInput, 0.125f);
            comparison.Update(second, in secondInput, 0.125f);
            comparison.CompleteFrame(0.125f);
            FlowAmbientParticle firstBorn = comparison.Actual[0];
            FlowAmbientParticle secondBorn = comparison.Actual[1];
            comparison.Remove(first);
            comparison.Remove(second);

            comparison.CompleteFrame(0.0625f);
            Assert.Equal(2, comparison.Actual.Count);
            Assert.Equal(firstBorn.X + firstBorn.Vx * 0.0625f, comparison.Actual[0].X);
            Assert.Equal(firstBorn.Y + firstBorn.Vy * 0.0625f, comparison.Actual[0].Y);
            Assert.Equal(firstBorn.Life - 0.0625f, comparison.Actual[0].Life);
            Assert.Equal(secondBorn.X + secondBorn.Vx * 0.0625f, comparison.Actual[1].X);
            Assert.Equal(secondBorn.Life - 0.0625f, comparison.Actual[1].Life);
            Assert.True(comparison.Actual[0].Life / firstBorn.MaxLife < 1f);

            // 仅完成帧，不再更新死亡源；两场在不同寿命下自然清空。
            for (int i = 0; i < 16; i++)
            {
                comparison.CompleteFrame(0.0625f);
                Assert.InRange(comparison.Actual.Count, 0, 2);
            }

            Assert.Empty(comparison.Actual);
            comparison.CompleteFrame(1f);
            Assert.Empty(comparison.Actual);
        }

        [Fact]
        public void ClearAmbient_PreservesFractionalRemainderRandomAndNextSourceSeed()
        {
            Comparison comparison = new Comparison();
            FlowEntity entity = MakeEntity();
            FlowEntity dying = MakeEntity();
            FlowEntity addedAfterClear = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot(7d, 9d);
            comparison.Add(entity);
            comparison.Add(dying);
            comparison.Update(entity, in snapshot, 0.1875f);
            comparison.Update(dying, in snapshot, 0.125f);
            comparison.CompleteFrame(0.1875f);
            FlowAmbientParticle firstBorn = comparison.Actual[0];
            comparison.Remove(dying);
            comparison.ClearAmbient();
            comparison.CompleteFrame(0.0625f);
            Assert.Empty(comparison.Actual);

            // 8 * 0.1875 留下半颗余量；清场后 8 * 0.0625 应立即补成一颗。
            comparison.Update(entity, in snapshot, 0.0625f);
            comparison.CompleteFrame(0.0625f);
            FlowAmbientParticle continued = Assert.Single(comparison.Actual);
            Assert.Equal(continued.MaxLife, continued.Life);
            Assert.NotEqual(firstBorn.MaxLife, continued.MaxLife);

            comparison.Add(addedAfterClear);
            FlowFrameInput newInput = MakeSnapshot(70d, 90d);
            comparison.Update(addedAfterClear, in newInput, 0.125f);
            comparison.CompleteFrame(0.125f);
            Assert.Equal(2, comparison.Actual.Count);

            // 清场不能重置实例种子；新源必须使用已消耗 0、1 之后的种子 2。
            FlowAmbientField seeded = new FlowAmbientField(32, 2);
            seeded.SpawnContent = newInput.Content;
            FlowParticleProfile profile = FlowParticleRules.Build(in newInput);
            seeded.Spawn(in profile, newInput.X, newInput.Y, newInput.Direction,
                FlowParticleRules.ColorFor(in newInput));
            AssertParticle(seeded.Get(0), comparison.Actual[1]);
        }

        [Fact]
        public void Update_ContradictoryEntityFields_ConsumesOnlyProvidedSnapshot()
        {
            Comparison comparison = new Comparison();
            FlowEntity entity = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot(10d, 20d, 0, FlowTemp.Hot,
                FlowContent.Empty, FlowState.Blocked,
                new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f));
            comparison.Add(entity);
            comparison.Update(entity, in snapshot, 0.125f);
            comparison.CompleteFrame(0.125f);
            FlowAmbientParticle born = Assert.Single(comparison.Actual);
            Assert.InRange(born.X, 10f, 11f);
            Assert.InRange(born.Y, 20f, 21f);
            FlowColor expectedColor = FlowParticleRules.ColorFor(in snapshot);
            Assert.Equal(expectedColor.R, born.R);
            Assert.Equal(expectedColor.G, born.G);
            Assert.Equal(expectedColor.B, born.B);
            Assert.InRange(born.MaxLife,
                FlowParticleTuning.LifeMin * FlowParticleTuning.HotLifeScale,
                FlowParticleTuning.LifeMax * FlowParticleTuning.HotLifeScale);

            // 身份键仍有效，但所有实体数据均不可读取。
            entity.Position = null;
            entity.Motion = null;
            entity.Properties = null;
            snapshot = MakeSnapshot(-30d, -40d, 2, FlowTemp.Cold, FlowContent.Large);
            comparison.Update(entity, in snapshot, 0.125f);
            comparison.CompleteFrame(0.125f);
            Assert.Equal(2, comparison.Actual.Count);
            Assert.InRange(comparison.Actual[1].X, -30f, -29f);
            Assert.InRange(comparison.Actual[1].Y, -40f, -39f);
        }

        [Fact]
        public void SeparateInstances_IsolateSeedsRemaindersClearResetAndFading()
        {
            Comparison first = new Comparison();
            Comparison second = new Comparison();
            FlowEntity sharedIdentity = MakeEntity();
            FlowEntity extra = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot(2d, 3d);
            first.Add(sharedIdentity);
            second.Add(sharedIdentity);
            first.Update(sharedIdentity, in snapshot, 0.1875f);
            second.Update(sharedIdentity, in snapshot, 0.1875f);
            first.CompleteFrame(0.1875f);
            second.CompleteFrame(0.1875f);
            AssertParticles(first.Actual, second.Actual);

            first.Add(extra);
            first.Update(extra, in snapshot, 0.125f);
            first.Remove(extra);
            first.ClearAmbient();
            first.Reset();
            second.Update(sharedIdentity, in snapshot, 0.0625f);
            second.CompleteFrame(0.0625f);
            Assert.Equal(2, second.Actual.Count);

            second.Remove(sharedIdentity);
            first.CompleteFrame(0.0625f);
            Assert.Empty(first.Actual);
            second.CompleteFrame(0.0625f);
            Assert.Equal(2, second.Actual.Count);
            first.ClearAmbient();
            first.Reset();
            second.CompleteFrame(0.0625f);
            Assert.Equal(2, second.Actual.Count);

            // 第二台机器的新源仍只消耗自己的种子 1。
            second.Add(extra);
            second.Update(extra, in snapshot, 0.125f);
            second.CompleteFrame(0.125f);
            FlowAmbientField seeded = new FlowAmbientField(32, 1);
            seeded.SpawnContent = snapshot.Content;
            FlowParticleProfile profile = FlowParticleRules.Build(in snapshot);
            seeded.Spawn(in profile, snapshot.X, snapshot.Y, snapshot.Direction,
                FlowParticleRules.ColorFor(in snapshot));
            AssertParticle(seeded.Get(0), second.Actual[0]);
        }

        [Fact]
        public void Reset_ClearsLiveAndFading_WithoutHoldingCallerOutput()
        {
            Comparison comparison = new Comparison();
            FlowEntity live = MakeEntity();
            FlowEntity dying = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot();
            comparison.Add(live);
            comparison.Add(dying);
            comparison.Update(live, in snapshot, 0.125f);
            comparison.Update(dying, in snapshot, 0.125f);
            comparison.Remove(dying);
            comparison.CompleteFrame(0.0625f);
            Assert.Equal(2, comparison.Actual.Count);
            List<FlowAmbientParticle> retained = new List<FlowAmbientParticle>(comparison.Actual);
            List<FlowAmbientParticle> alternate = new List<FlowAmbientParticle>
            {
                new FlowAmbientParticle { X = -123f }
            };
            comparison.System.CompleteFrame(0f, alternate);
            comparison.Reference.CompleteFrame(0f, comparison.Expected);
            AssertParticles(comparison.Expected, alternate);
            AssertParticles(retained, alternate);
            AssertParticles(retained, comparison.Actual);

            comparison.Reset();
            comparison.Reset();
            AssertParticles(retained, comparison.Actual);
            AssertParticles(retained, alternate);
            comparison.CompleteFrame(1f);
            Assert.Empty(comparison.Actual);
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Update(live, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Update(live, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Remove(dying));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Remove(dying));
        }

        [Fact]
        public void Add_DuplicateReplacesSlotConsumesSeed_AndMissingKeysThrow()
        {
            Comparison comparison = new Comparison();
            FlowEntity entity = MakeEntity();
            FlowEntity missing = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot();
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Update(missing, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Update(missing, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Remove(missing));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Remove(missing));
            comparison.Add(entity);
            comparison.Update(entity, in snapshot, 0.1875f);
            comparison.CompleteFrame(0.1875f);
            FlowAmbientParticle old = Assert.Single(comparison.Actual);

            comparison.Add(entity);
            comparison.CompleteFrame(0.0625f);
            Assert.Empty(comparison.Actual);
            comparison.Update(entity, in snapshot, 0.0625f);
            comparison.CompleteFrame(0.0625f);
            Assert.Empty(comparison.Actual);
            comparison.Update(entity, in snapshot, 0.0625f);
            comparison.CompleteFrame(0.0625f);
            FlowAmbientParticle replacement = Assert.Single(comparison.Actual);
            Assert.NotEqual(old.MaxLife, replacement.MaxLife);
            comparison.Remove(entity);
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Update(entity, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Update(entity, in snapshot, 0.125f));
            Assert.Throws<KeyNotFoundException>(() => comparison.System.Remove(entity));
            Assert.Throws<KeyNotFoundException>(() => comparison.Reference.Remove(entity));
            comparison.CompleteFrame(0.0625f);
            Assert.Single(comparison.Actual);
        }

        [Fact]
        public void CompleteFrame_Above512AfterMiddleRemovalAndNewSource_MatchesAllAndPrefix()
        {
            Comparison comparison = new Comparison();
            FlowEntity[] entities = new FlowEntity[65];
            FlowFrameInput[] snapshots = new FlowFrameInput[65];
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = MakeEntity();
                snapshots[i] = MakeSnapshot(i * 3d, i * 5d);
                comparison.Add(entities[i]);
                comparison.Update(entities[i], in snapshots[i], 1f);
            }

            comparison.CompleteFrame(1f);
            Assert.Equal(520, comparison.Actual.Count);
            for (int i = 0; i < comparison.Actual.Count; i++)
            {
                Assert.Equal(comparison.Actual[i].MaxLife, comparison.Actual[i].Life);
            }

            comparison.Remove(entities[32]);
            FlowEntity added = MakeEntity();
            FlowFrameInput addedInput = MakeSnapshot(1000d, 2000d);
            comparison.Add(added);
            for (int i = 0; i < entities.Length; i++)
            {
                if (i != 32)
                {
                    comparison.Update(entities[i], in snapshots[i], 0.125f);
                }
            }

            comparison.Update(added, in addedInput, 1f);
            comparison.CompleteFrame(0.125f);
            Assert.True(comparison.Actual.Count > 512);
            // 完整输出已在 CompleteFrame 对照，另明确验证渲染会消费的前 512 项。
            for (int i = 0; i < 512; i++)
            {
                AssertParticle(comparison.Expected[i], comparison.Actual[i]);
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UpdateAndCompleteFrame_WarmedActualSpawn_DoesNotAllocate(bool includeFading)
        {
            FlowParticleSystem system = new FlowParticleSystem();
            FlowEntity live = MakeEntity();
            FlowEntity warmupDying = MakeEntity();
            FlowFrameInput snapshot = MakeSnapshot(3d, 4d);
            List<FlowAmbientParticle> output = new List<FlowAmbientParticle>(128);
            system.Add(live);
            system.Add(warmupDying);
            system.Update(warmupDying, in snapshot, 1f);
            system.Remove(warmupDying);

            // 同线程预热规则、Random、活场出生与淡出移除，容量在窗口前固定。
            for (int i = 0; i < 256; i++)
            {
                system.Update(live, in snapshot, 0.125f);
                system.CompleteFrame(0.125f, output);
            }

            if (includeFading)
            {
                FlowEntity dying = MakeEntity();
                system.Add(dying);
                system.Update(dying, in snapshot, 1f);
                system.Remove(dying);
                int liveCount = output.Count;
                system.CompleteFrame(0f, output);
                Assert.Equal(liveCount + 8, output.Count);
            }

            int capacity = output.Capacity;
            int births = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++)
            {
                system.Update(live, in snapshot, 0.125f);
                system.CompleteFrame(0.125f, output);
                for (int j = 0; j < output.Count; j++)
                {
                    if (output[j].Life == output[j].MaxLife)
                    {
                        births++;
                    }
                }
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(256, births);
            Assert.Equal(capacity, output.Capacity);
            Assert.Equal(0L, allocated);
        }

        private static FlowEntity MakeEntity()
        {
            return new FlowEntity(new FlowPos(-1000d, 2000d),
                new FlowMotionData(3, 99d), new FlowProperties(0d, -100d));
        }

        private static FlowFrameInput MakeSnapshot(
            double x = 0d, double y = 0d, int direction = 1,
            FlowTemp temp = FlowTemp.Normal, FlowContent content = FlowContent.Large,
            FlowState state = FlowState.Flow, FlowComposition? composition = null)
        {
            // 不命中现有测试 modifier 的哨兵速度 -98765.5，不注册任何新 modifier。
            return new FlowFrameInput(x, y, direction, 2d,
                composition ?? FlowComposition.PureWater, temp, content, state);
        }

        private static void AssertParticles(
            List<FlowAmbientParticle> expected, List<FlowAmbientParticle> actual)
        {
            Assert.Equal(expected.Count, actual.Count);
            for (int i = 0; i < expected.Count; i++)
            {
                AssertParticle(expected[i], actual[i]);
            }
        }

        private static void AssertParticle(FlowAmbientParticle expected, FlowAmbientParticle actual)
        {
            Assert.Equal(expected.X, actual.X);
            Assert.Equal(expected.Y, actual.Y);
            Assert.Equal(expected.Vx, actual.Vx);
            Assert.Equal(expected.Vy, actual.Vy);
            Assert.Equal(expected.Life, actual.Life);
            Assert.Equal(expected.MaxLife, actual.MaxLife);
            Assert.Equal(expected.R, actual.R);
            Assert.Equal(expected.G, actual.G);
            Assert.Equal(expected.B, actual.B);
            Assert.Equal(expected.Alpha, actual.Alpha);
            Assert.Equal(expected.SizePx, actual.SizePx);
        }

        // 只负责把相同调用序列送给两方；参考算法不调用此包装或新组件。
        private sealed class Comparison
        {
            public readonly FlowParticleSystem System = new FlowParticleSystem();
            public readonly BaselineModel Reference = new BaselineModel();
            public readonly List<FlowAmbientParticle> Actual = new List<FlowAmbientParticle>();
            public readonly List<FlowAmbientParticle> Expected = new List<FlowAmbientParticle>();

            public void Add(FlowEntity entity)
            {
                System.Add(entity);
                Reference.Add(entity);
            }

            public void Remove(FlowEntity entity)
            {
                System.Remove(entity);
                Reference.Remove(entity);
            }

            public void Update(FlowEntity entity, in FlowFrameInput snapshot, float deltaTime)
            {
                System.Update(entity, in snapshot, deltaTime);
                Reference.Update(entity, in snapshot, deltaTime);
            }

            public void CompleteFrame(float deltaTime)
            {
                System.CompleteFrame(deltaTime, Actual);
                Reference.CompleteFrame(deltaTime, Expected);
                AssertParticles(Expected, Actual);
            }

            public void ClearAmbient()
            {
                System.ClearAmbient();
                Reference.ClearAmbient();
            }

            public void Reset()
            {
                System.Reset();
                Reference.Reset();
            }
        }

        // 来源：git show 20a519b4c55a9b90c98dfc57e3aa23f94e042a2f:src/Mod/Visual/FlowVisualLayer.cs
        // 仅保留旧粒子编排，使用原场与规则；容器操作、枚举和运算顺序均不重写。
        private sealed class BaselineModel
        {
            private sealed class ParticleSlot
            {
                public FlowAmbientField Field;
                public float Accum;
            }

            private readonly Dictionary<FlowEntity, ParticleSlot> _particles
                = new Dictionary<FlowEntity, ParticleSlot>();
            private readonly List<FlowAmbientField> _fading = new List<FlowAmbientField>();
            private int _seed;

            public void Add(FlowEntity entity)
            {
                ParticleSlot slot = new ParticleSlot();
                slot.Field = new FlowAmbientField(32, _seed++);
                slot.Accum = 0f;
                _particles[entity] = slot;
            }

            public void Remove(FlowEntity entity)
            {
                ParticleSlot slot = _particles[entity];
                _particles.Remove(entity);
                _fading.Add(slot.Field);
            }

            public void Update(FlowEntity entity, in FlowFrameInput snapshot, float deltaTime)
            {
                ParticleSlot slot = _particles[entity];
                FlowParticleProfile profile = FlowParticleRules.Build(in snapshot);
                slot.Field.SpawnContent = snapshot.Content;
                slot.Field.Advance(deltaTime);
                slot.Accum += profile.SpawnRate * deltaTime;
                while (slot.Accum >= 1f)
                {
                    slot.Accum -= 1f;
                    slot.Field.Spawn(
                        profile,
                        snapshot.X,
                        snapshot.Y,
                        snapshot.Direction,
                        FlowParticleRules.ColorFor(in snapshot));
                }
            }

            public void CompleteFrame(float deltaTime, List<FlowAmbientParticle> output)
            {
                for (int i = _fading.Count - 1; i >= 0; i--)
                {
                    FlowAmbientField field = _fading[i];
                    field.Advance(deltaTime);
                    if (field.Count == 0)
                    {
                        _fading.RemoveAt(i);
                    }
                }

                output.Clear();
                foreach (ParticleSlot slot in _particles.Values)
                {
                    AppendDots(slot.Field, output);
                }

                for (int i = 0; i < _fading.Count; i++)
                {
                    AppendDots(_fading[i], output);
                }
            }

            public void ClearAmbient()
            {
                foreach (ParticleSlot slot in _particles.Values)
                {
                    slot.Field.Clear();
                }

                for (int i = 0; i < _fading.Count; i++)
                {
                    _fading[i].Clear();
                }

                _fading.Clear();
            }

            public void Reset()
            {
                _particles.Clear();
                _fading.Clear();
            }

            private static void AppendDots(FlowAmbientField field, List<FlowAmbientParticle> output)
            {
                int count = field.Count;
                for (int i = 0; i < count; i++)
                {
                    output.Add(field.Get(i));
                }
            }
        }
    }
}
