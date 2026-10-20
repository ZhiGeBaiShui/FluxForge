using System;
using Xunit;
using ProbablyBagMod.Presentation.Flow.Particles;
using ProbablyBagMod.Presentation.Flow.Visual;

namespace ProbablyBagMod.Tests
{
    /// <summary>
    /// 流源常驻粒子纯逻辑层（<c>src/Presentation/Flow/FlowParticle*</c>）的单测。
    ///
    /// 覆盖四段：
    ///   1. Profile：默认值、温度三档倍率、modifier 扩展点；
    ///   2. ColorFor：水 / 50% 毒的逐字节期望；
    ///   3. Spawn：数量、bbox、寿命/初速范围、反向后偏、颜色传递、确定性；
    ///   4. Advance：积分、寿命移除、容量上限、扩散加速淡出、swap-remove、0 分配。
    ///
    /// 这一层零 Unity 依赖；测试项目把 <c>src/Presentation/**</c> 源码直接编入。
    /// </summary>
    public class FlowParticleTests
    {
        /// <summary>测试 modifier 的哨兵速度：只有这个输入才会被改写，避免污染其它测试。</summary>
        private const float MarkerSpeed = -98765.5f;

        /// <summary>浮点比较容差（速度/寿命/坐标都是毫秒级的小数运算）。</summary>
        private const float Tolerance = 1e-4f;

        // ── Profile / modifier ─────────────────────────────────────

        [Fact]
        public void Build_DefaultProfile_MatchesTuning()
        {
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));

            Assert.Equal(FlowParticleTuning.SpeedMin, profile.SpeedMin);
            Assert.Equal(FlowParticleTuning.SpeedMax, profile.SpeedMax);
            Assert.Equal(FlowParticleTuning.LifeMin, profile.LifeMin);
            Assert.Equal(FlowParticleTuning.LifeMax, profile.LifeMax);
            Assert.Equal(FlowParticleTuning.AlphaStart, profile.AlphaStart);
            Assert.Equal(FlowParticleTuning.SizeCells, profile.SizeCells);
            Assert.Equal(FlowParticleTuning.BackwardBias, profile.BackwardBias);
            Assert.Equal(FlowParticleTuning.SpawnRate, profile.SpawnRate);
            Assert.Equal(FlowParticleTuning.SpreadRadius, profile.SpreadRadius);
        }

        [Theory]
        [InlineData(FlowTemp.Cold, 0.6f, 1.2f)]
        [InlineData(FlowTemp.Normal, 1.0f, 1.0f)]
        [InlineData(FlowTemp.Hot, 1.6f, 0.8f)]
        public void Build_Temperature_ScalesSpeedAndLife(FlowTemp temp, float speedScale, float lifeScale)
        {
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(temp));

            Assert.Equal(FlowParticleTuning.SpeedMin * speedScale, profile.SpeedMin, 5);
            Assert.Equal(FlowParticleTuning.SpeedMax * speedScale, profile.SpeedMax, 5);
            Assert.Equal(FlowParticleTuning.LifeMin * lifeScale, profile.LifeMin, 5);
            Assert.Equal(FlowParticleTuning.LifeMax * lifeScale, profile.LifeMax, 5);

            // 温度这一轮只动速度/寿命，其余字段必须原样保留。
            Assert.Equal(FlowParticleTuning.AlphaStart, profile.AlphaStart);
            Assert.Equal(FlowParticleTuning.SizeCells, profile.SizeCells);
            Assert.Equal(FlowParticleTuning.BackwardBias, profile.BackwardBias);
            Assert.Equal(FlowParticleTuning.SpawnRate, profile.SpawnRate);
            Assert.Equal(FlowParticleTuning.SpreadRadius, profile.SpreadRadius);
        }

        [Fact]
        public void Build_IsRepeatable_AndDoesNotMutateInput()
        {
            FlowParticleInput input = MakeInput(FlowTemp.Hot);

            FlowParticleProfile first = FlowParticleRules.Build(in input);
            FlowParticleProfile second = FlowParticleRules.Build(in input);

            Assert.Equal(first.SpeedMin, second.SpeedMin);
            Assert.Equal(first.SpeedMax, second.SpeedMax);
            Assert.Equal(first.LifeMin, second.LifeMin);
            Assert.Equal(first.LifeMax, second.LifeMax);
            Assert.Equal(FlowTemp.Hot, input.Temp);
        }

        [Fact]
        public void Register_Modifier_IsAppliedByBuild()
        {
            // 证明扩展点可用：注册一个只在哨兵速度下生效的测试 modifier，
            // 它既不改全局默认值，也不会影响其它测试的 profile。
            FlowParticleRules.Register(new MarkerModifier());

            FlowParticleProfile marked = FlowParticleRules.Build(
                MakeInput(FlowTemp.Normal, MarkerSpeed));
            FlowParticleProfile normal = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));

            Assert.Equal(1234f, marked.SpawnRate);
            Assert.Equal(9f, marked.SpeedMin);
            Assert.Equal(10f, marked.SpeedMax);

            // 未命中哨兵的输入不受影响。
            Assert.Equal(FlowParticleTuning.SpawnRate, normal.SpawnRate);
            Assert.Equal(FlowParticleTuning.SpeedMin, normal.SpeedMin);
        }

        [Fact]
        public void Register_Null_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => FlowParticleRules.Register(null));
        }

        // ── ColorFor ───────────────────────────────────────────────

        [Fact]
        public void ColorFor_Water_IsBodyMixedTenPercentWhite()
        {
            FlowParticleInput input = new FlowParticleInput(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                0f);

            // body = #27C7E6 = (39,199,230)；mix(white,0.10)：
            // R=60.6→61，G=204.6→205，B=232.5→232（银行家舍入取偶）。
            FlowColor color = FlowParticleRules.ColorFor(in input);

            Assert.Equal((byte)61, color.R);
            Assert.Equal((byte)205, color.G);
            Assert.Equal((byte)232, color.B);
        }

        [Fact]
        public void ColorFor_HalfPoison_IsBodyMixedTenPercentWhite()
        {
            FlowComposition halfPoison = new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f);
            FlowParticleInput input = new FlowParticleInput(
                halfPoison,
                FlowTemp.Hot,
                FlowContent.Mid,
                FlowState.Split,
                0f);

            // body 是 (97,146,242)；mix(white,0.10)：
            // R=112.8→113，G=156.9→157，B=243.3→243。
            FlowColor color = FlowParticleRules.ColorFor(in input);

            Assert.Equal((byte)113, color.R);
            Assert.Equal((byte)157, color.G);
            Assert.Equal((byte)243, color.B);
        }

        [Fact]
        public void ColorFor_IgnoresTempContentStateAndSpeed()
        {
            FlowComposition water = FlowComposition.PureWater;
            FlowParticleInput cold = new FlowParticleInput(
                water, FlowTemp.Cold, FlowContent.Empty, FlowState.Blocked, 123f);
            FlowParticleInput hot = new FlowParticleInput(
                water, FlowTemp.Hot, FlowContent.Large, FlowState.Collect, -456f);

            FlowColor coldColor = FlowParticleRules.ColorFor(in cold);
            FlowColor hotColor = FlowParticleRules.ColorFor(in hot);

            Assert.Equal(coldColor.R, hotColor.R);
            Assert.Equal(coldColor.G, hotColor.G);
            Assert.Equal(coldColor.B, hotColor.B);
        }

        // ── Spawn ──────────────────────────────────────────────────

        [Fact]
        public void Spawn_AddsParticles()
        {
            FlowAmbientField field = new FlowAmbientField(8, 1);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(10, 20, 30);

            for (int i = 0; i < 5; i++)
            {
                field.Spawn(in profile, 3d, 4d, 1, in color);
            }

            Assert.Equal(5, field.Count);
        }

        [Fact]
        public void Spawn_LifeWithinProfileRange()
        {
            FlowAmbientField field = new FlowAmbientField(512, 1);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            for (int i = 0; i < 500; i++)
            {
                field.Spawn(in profile, 0d, 0d, 1, in color);
            }

            for (int i = 0; i < field.Count; i++)
            {
                FlowAmbientParticle particle = field.Get(i);
                Assert.InRange(particle.MaxLife, profile.LifeMin, profile.LifeMax);
                Assert.Equal(particle.MaxLife, particle.Life);
            }
        }

        [Fact]
        public void Spawn_SpeedWithinProfileRange()
        {
            FlowAmbientField field = new FlowAmbientField(512, 2);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            for (int i = 0; i < 500; i++)
            {
                field.Spawn(in profile, 0d, 0d, 1, in color);
            }

            for (int i = 0; i < field.Count; i++)
            {
                FlowAmbientParticle particle = field.Get(i);
                float speed = MathF.Sqrt(particle.Vx * particle.Vx + particle.Vy * particle.Vy);

                Assert.True(
                    speed >= profile.SpeedMin - Tolerance && speed <= profile.SpeedMax + Tolerance,
                    "初速越界：" + speed + "，期望 [" + profile.SpeedMin + ", " + profile.SpeedMax + "]。");
            }
        }

        [Fact]
        public void Spawn_BackwardBias_OpposesMovementDirection()
        {
            // 四次采样平均：径向分量在对称 bbox 上相互抵消，剩下的应该指向运动反方向。
            FlowAmbientField field = new FlowAmbientField(512, 3);
            field.SpawnContent = FlowContent.Mid;
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            AssertAverageBackward(field, in profile, in color, 1, true, false);  // Right：平均 Vx < 0
            AssertAverageBackward(field, in profile, in color, 0, false, true);  // Up：平均 Vy > 0
            AssertAverageBackward(field, in profile, in color, 2, false, false); // Down：平均 Vy < 0
            AssertAverageBackward(field, in profile, in color, 3, true, true);   // Left：平均 Vx > 0
        }

        [Fact]
        public void Spawn_RespectsContentBounds()
        {
            // SPEC §1 右向 bbox：空 6×1 @(5,7)、少 8×6 @(4,5)、中 8×8 @(4,4)、多 12×8 @(2,4)。
            AssertContentBounds(FlowContent.Empty, 5, 11, 7, 8, false);
            AssertContentBounds(FlowContent.Small, 4, 12, 5, 11, true);
            AssertContentBounds(FlowContent.Mid, 4, 12, 4, 12, true);
            AssertContentBounds(FlowContent.Large, 2, 14, 4, 12, true);
        }

        [Fact]
        public void Spawn_BoundsUseUnrotatedBase_EvenForVerticalDirection()
        {
            // 契约：bbox 固定用 SPEC §1 的右向基准，不随方向旋转；
            // large 向上/向下时仍然是 12×8 @(2,4)，方向只影响速度后偏。
            const double CellX = 10d;
            const double CellY = 20d;

            FlowAmbientField field = new FlowAmbientField(512, 4);
            field.SpawnContent = FlowContent.Large;
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            int[] verticalDirs = { 0, 2 };
            foreach (int dir in verticalDirs)
            {
                field.Clear();
                for (int i = 0; i < 400; i++)
                {
                    field.Spawn(in profile, CellX, CellY, dir, in color);
                }

                for (int i = 0; i < field.Count; i++)
                {
                    FlowAmbientParticle particle = field.Get(i);
                    AssertWithinPixelBounds(particle, CellX, CellY, 2, 14, 4, 12);
                }
            }
        }

        [Fact]
        public void Spawn_EdgeAndInterior_RatioIsAboutThirtyPercent()
        {
            // 30% 边缘 + 70% 内部是行为契约；用固定种子采样 800 次，允许少量统计波动。
            FlowAmbientField field = new FlowAmbientField(1024, 5);
            field.SpawnContent = FlowContent.Mid;
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            const double CellX = 0d;
            const double CellY = 0d;
            const int Samples = 800;

            int edgeCount = 0;
            for (int i = 0; i < Samples; i++)
            {
                field.Spawn(in profile, CellX, CellY, 1, in color);
                FlowAmbientParticle particle = field.Get(field.Count - 1);
                if (IsOnBBoxEdge(particle, CellX, CellY, 4, 12, 4, 12))
                {
                    edgeCount++;
                }
            }

            double ratio = (double)edgeCount / Samples;
            Assert.InRange(ratio, 0.24d, 0.36d);
        }

        [Fact]
        public void Spawn_PassesColorThrough()
        {
            FlowAmbientField field = new FlowAmbientField(64, 6);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(0x12, 0x34, 0x56);

            for (int i = 0; i < 40; i++)
            {
                field.Spawn(in profile, 0d, 0d, 1, in color);
            }

            for (int i = 0; i < field.Count; i++)
            {
                FlowAmbientParticle particle = field.Get(i);
                Assert.Equal(color.R, particle.R);
                Assert.Equal(color.G, particle.G);
                Assert.Equal(color.B, particle.B);
            }
        }

        [Fact]
        public void Spawn_SameSeed_IsReproducible()
        {
            FlowAmbientField first = new FlowAmbientField(64, 42);
            FlowAmbientField second = new FlowAmbientField(64, 42);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Hot));
            FlowColor color = new FlowColor(7, 8, 9);

            for (int i = 0; i < 50; i++)
            {
                first.Spawn(in profile, 2d, 3d, 1, in color);
                second.Spawn(in profile, 2d, 3d, 1, in color);
            }

            Assert.Equal(first.Count, second.Count);
            for (int i = 0; i < first.Count; i++)
            {
                FlowAmbientParticle a = first.Get(i);
                FlowAmbientParticle b = second.Get(i);

                Assert.Equal(a.X, b.X);
                Assert.Equal(a.Y, b.Y);
                Assert.Equal(a.Vx, b.Vx);
                Assert.Equal(a.Vy, b.Vy);
                Assert.Equal(a.Life, b.Life);
                Assert.Equal(a.MaxLife, b.MaxLife);
            }
        }

        [Fact]
        public void Spawn_FullCapacity_OverwritesOldest()
        {
            FlowAmbientField field = new FlowAmbientField(2, 7);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            profile.LifeMin = 10f;
            profile.LifeMax = 10f;

            FlowColor red = new FlowColor(255, 0, 0);
            FlowColor green = new FlowColor(0, 255, 0);
            FlowColor blue = new FlowColor(0, 0, 255);

            field.Spawn(in profile, 0d, 0d, 1, in red);
            field.Spawn(in profile, 0d, 0d, 1, in green);
            field.Spawn(in profile, 0d, 0d, 1, in blue);

            Assert.Equal(2, field.Count);

            bool sawRed = false;
            bool sawGreen = false;
            bool sawBlue = false;
            for (int i = 0; i < field.Count; i++)
            {
                FlowAmbientParticle particle = field.Get(i);
                if (particle.R == 255 && particle.G == 0 && particle.B == 0)
                {
                    sawRed = true;
                }
                else if (particle.R == 0 && particle.G == 255 && particle.B == 0)
                {
                    sawGreen = true;
                }
                else if (particle.R == 0 && particle.G == 0 && particle.B == 255)
                {
                    sawBlue = true;
                }
            }

            Assert.False(sawRed, "最老的红色粒子应被覆盖。");
            Assert.True(sawGreen);
            Assert.True(sawBlue);
        }

        // ── Advance ────────────────────────────────────────────────

        [Fact]
        public void Advance_IntegratesPositionAndLife()
        {
            FlowAmbientField field = new FlowAmbientField(4, 8);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            profile.SpeedMin = 1f;
            profile.SpeedMax = 1f;
            profile.LifeMin = 10f;
            profile.LifeMax = 10f;
            profile.SpreadRadius = 100f;
            profile.BackwardBias = 0.3f;

            FlowColor color = new FlowColor(1, 2, 3);
            field.Spawn(in profile, 5d, 6d, 1, in color);

            FlowAmbientParticle before = field.Get(0);
            field.Advance(0.1f);
            FlowAmbientParticle after = field.Get(0);

            Assert.Equal(before.X + before.Vx * 0.1f, after.X, 5);
            Assert.Equal(before.Y + before.Vy * 0.1f, after.Y, 5);
            Assert.Equal(before.Life - 0.1f, after.Life, 5);
        }

        [Fact]
        public void Advance_RemovesParticlesWhenLifeExpires()
        {
            FlowAmbientField field = new FlowAmbientField(8, 9);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            profile.LifeMin = 0.1f;
            profile.LifeMax = 0.1f;
            profile.SpreadRadius = 100f;

            FlowColor color = new FlowColor(1, 2, 3);
            for (int i = 0; i < 3; i++)
            {
                field.Spawn(in profile, 0d, 0d, 1, in color);
            }

            field.Advance(0.05f);
            Assert.Equal(3, field.Count);

            field.Advance(0.06f);
            Assert.Equal(0, field.Count);
        }

        [Fact]
        public void Advance_NonPositiveDelta_FreezesParticles()
        {
            FlowAmbientField field = new FlowAmbientField(4, 10);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);
            field.Spawn(in profile, 0d, 0d, 1, in color);

            FlowAmbientParticle before = field.Get(0);

            field.Advance(0f);
            field.Advance(-1f);
            field.Advance(float.NaN);

            FlowAmbientParticle after = field.Get(0);
            Assert.Equal(1, field.Count);
            Assert.Equal(before.X, after.X);
            Assert.Equal(before.Y, after.Y);
            Assert.Equal(before.Life, after.Life);
        }

        [Fact]
        public void Advance_SpreadRadius_AcceleratesFade()
        {
            // 同一份随机序列、同一批粒子，只把扩散半径从 100 格缩到 0.05 格：
            // 后者应明显更早清空，证明「超出半径后加速淡出」生效。
            FlowParticleProfile wideProfile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            wideProfile.SpeedMin = 1f;
            wideProfile.SpeedMax = 1f;
            wideProfile.LifeMin = 10f;
            wideProfile.LifeMax = 10f;
            wideProfile.SpreadRadius = 100f;

            FlowParticleProfile narrowProfile = wideProfile;
            narrowProfile.SpreadRadius = 0.05f;

            FlowAmbientField wide = new FlowAmbientField(64, 11);
            FlowAmbientField narrow = new FlowAmbientField(64, 11);
            wide.SpawnContent = FlowContent.Mid;
            narrow.SpawnContent = FlowContent.Mid;

            FlowColor color = new FlowColor(1, 2, 3);
            for (int i = 0; i < 16; i++)
            {
                wide.Spawn(in wideProfile, 0d, 0d, 1, in color);
                narrow.Spawn(in narrowProfile, 0d, 0d, 1, in color);
            }

            int wideSteps = StepsUntilEmpty(wide, 0.01f, 2000);
            int narrowSteps = StepsUntilEmpty(narrow, 0.01f, 2000);

            Assert.True(narrowSteps < wideSteps, "小半径应比大半径更早清空。");
            Assert.True(narrowSteps < 500, "超出半径后应快速淡完，实际用了 " + narrowSteps + " 步。");
            Assert.True(wideSteps > 900, "未超半径的粒子应接近 10 秒寿命，实际用了 " + wideSteps + " 步。");
        }

        [Fact]
        public void Advance_SwapRemove_KeepsRemainingParticlesAlive()
        {
            // 两颗短命 + 一颗长命：短命被移除后，长命粒子必须被搬回并继续推进。
            FlowAmbientField field = new FlowAmbientField(8, 12);
            FlowParticleProfile shortProfile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            shortProfile.LifeMin = 0.05f;
            shortProfile.LifeMax = 0.05f;
            shortProfile.SpreadRadius = 100f;

            FlowParticleProfile longProfile = shortProfile;
            longProfile.LifeMin = 10f;
            longProfile.LifeMax = 10f;

            FlowColor color = new FlowColor(1, 2, 3);
            field.Spawn(in shortProfile, 0d, 0d, 1, in color);
            field.Spawn(in longProfile, 0d, 0d, 1, in color);
            field.Spawn(in shortProfile, 0d, 0d, 1, in color);

            field.Advance(0.1f);

            Assert.Equal(1, field.Count);
            Assert.True(field.Get(0).Life > 9f);
        }

        [Fact]
        public void Clear_RemovesAllParticles()
        {
            FlowAmbientField field = new FlowAmbientField(8, 13);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);

            for (int i = 0; i < 5; i++)
            {
                field.Spawn(in profile, 0d, 0d, 1, in color);
            }

            field.Clear();

            Assert.Equal(0, field.Count);
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Get(0));
        }

        [Fact]
        public void Get_OutOfRange_Throws()
        {
            FlowAmbientField field = new FlowAmbientField(4, 14);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            FlowColor color = new FlowColor(1, 2, 3);
            field.Spawn(in profile, 0d, 0d, 1, in color);

            Assert.Throws<ArgumentOutOfRangeException>(() => field.Get(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Get(1));

            field.Clear();
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Get(0));
        }

        [Fact]
        public void Ctor_NonPositiveCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowAmbientField(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FlowAmbientField(-1));
        }

        [Fact]
        public void SpawnAndAdvance_SteadyState_DoesNotAllocate()
        {
            // 稳态 0 分配是硬要求：容量满后覆盖最老、swap-remove、Get 都不能分配。
            FlowAmbientField field = new FlowAmbientField(32, 15);
            FlowParticleProfile profile = FlowParticleRules.Build(MakeInput(FlowTemp.Normal));
            profile.LifeMin = 10f;
            profile.LifeMax = 10f;
            profile.SpreadRadius = 100f;
            FlowColor color = new FlowColor(1, 2, 3);

            for (int i = 0; i < 32; i++)
            {
                field.Spawn(in profile, 1d, 2d, 1, in color);
            }

            // 预热：让 JIT、Random 以及各分支在测量前就位。
            float warmup = 0f;
            for (int i = 0; i < 200; i++)
            {
                field.Spawn(in profile, 1d, 2d, 1, in color);
                field.Advance(0.001f);
                warmup += field.Get(0).X;
            }

            Assert.True(warmup > 0f);

            long before = GC.GetAllocatedBytesForCurrentThread();

            float sum = 0f;
            for (int i = 0; i < 500; i++)
            {
                field.Spawn(in profile, 1d, 2d, 1, in color);
                field.Advance(0.001f);
                FlowAmbientParticle particle = field.Get(0);
                sum += particle.X + particle.Y;
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(sum > 0f);
            Assert.Equal(0L, allocated);
        }

        // ── 辅助 ───────────────────────────────────────────────────

        /// <summary>构造 normal 默认输入；哨兵速度用于测试 Register 扩展点。</summary>
        private static FlowParticleInput MakeInput(FlowTemp temp, float speed = 0f)
        {
            return new FlowParticleInput(
                FlowComposition.PureWater,
                temp,
                FlowContent.Large,
                FlowState.Flow,
                speed);
        }

        /// <summary>对某个方向采样，推进一小段后断言平均位移指向运动反方向。</summary>
        private static void AssertAverageBackward(
            FlowAmbientField field,
            in FlowParticleProfile profile,
            in FlowColor color,
            int dir,
            bool horizontal,
            bool expectPositive)
        {
            field.Clear();

            // 固定 10 秒寿命、100 格扩散半径，保证推进后没有粒子提前死亡/加速淡出，
            // 平均位移只反映初速方向。
            FlowParticleProfile fixedProfile = profile;
            fixedProfile.LifeMin = 10f;
            fixedProfile.LifeMax = 10f;
            fixedProfile.SpreadRadius = 100f;

            const int Samples = 400;
            const float DeltaTime = 0.1f;

            double before = 0d;
            for (int i = 0; i < Samples; i++)
            {
                field.Spawn(in fixedProfile, 0d, 0d, dir, in color);
                FlowAmbientParticle particle = field.Get(i);
                before += horizontal ? particle.X : particle.Y;
            }

            field.Advance(DeltaTime);

            double after = 0d;
            for (int i = 0; i < field.Count; i++)
            {
                FlowAmbientParticle particle = field.Get(i);
                after += horizontal ? particle.X : particle.Y;
            }

            double averageDisplacement = (after - before) / field.Count;
            if (expectPositive)
            {
                Assert.True(
                    averageDisplacement > 0.005d,
                    "方向 " + dir + " 的平均位移应为正，实际 " + averageDisplacement + "。");
            }
            else
            {
                Assert.True(
                    averageDisplacement < -0.005d,
                    "方向 " + dir + " 的平均位移应为负，实际 " + averageDisplacement + "。");
            }
        }

        /// <summary>按 content/dir=right 采样，断言所有粒子落在 SPEC bbox 内，并检查边缘比例。</summary>
        private static void AssertContentBounds(
            FlowContent content,
            int x0,
            int x1,
            int y0,
            int y1,
            bool expectThirtyPercentEdge)
        {
            const double CellX = 7d;
            const double CellY = 9d;
            const int Samples = 400;

            FlowAmbientField field = new FlowAmbientField(512, 20);
            field.SpawnContent = content;
            FlowParticleProfile profile = FlowParticleRules.Build(
                new FlowParticleInput(
                    FlowComposition.PureWater,
                    FlowTemp.Normal,
                    content,
                    FlowState.Flow,
                    0f));
            FlowColor color = new FlowColor(1, 2, 3);

            int edgeCount = 0;
            for (int i = 0; i < Samples; i++)
            {
                field.Spawn(in profile, CellX, CellY, 1, in color);
                FlowAmbientParticle particle = field.Get(i);
                AssertWithinPixelBounds(particle, CellX, CellY, x0, x1, y0, y1);

                if (IsOnBBoxEdge(particle, CellX, CellY, x0, x1, y0, y1))
                {
                    edgeCount++;
                }
            }

            double ratio = (double)edgeCount / Samples;
            if (expectThirtyPercentEdge)
            {
                Assert.InRange(ratio, 0.24d, 0.36d);
            }
            else
            {
                // 空档 6×1 没有内部像素，整条都是边缘。
                Assert.Equal(1.0d, ratio, 6);
            }
        }

        [Fact]
        public void Spawn_CarriesAlphaAndSizeFromProfile()
        {
            // 扩展点回归：modifier 改过的 AlphaStart / SizeCells 必须落到粒子上，
            // 否则渲染层会回退到全局 tuning 常量，属性 modifier 对外观静默无效。
            FlowAmbientField field = new FlowAmbientField(8, 123);
            FlowParticleProfile profile = new FlowParticleProfile
            {
                SpeedMin = 0.6f,
                SpeedMax = 0.6f,
                LifeMin = 0.5f,
                LifeMax = 0.5f,
                AlphaStart = 0.2f,
                SizeCells = 1f / 32f,
                BackwardBias = 0f,
                SpawnRate = 0f,
                SpreadRadius = 1f,
            };

            FlowColor color = new FlowColor(1, 2, 3);
            field.Spawn(in profile, 0d, 0d, 1, in color);

            FlowAmbientParticle particle = field.Get(0);
            Assert.Equal((byte)51, particle.Alpha);   // round(0.20 × 255) = 51
            Assert.Equal((byte)1, particle.SizePx);   // round(0.5 px) 夹到最小 1
        }

        [Fact]
        public void Spawn_DefaultProfileUsesTuningAlphaAndOnePixel()
        {
            FlowAmbientField field = new FlowAmbientField(8, 7);
            FlowParticleInput input = new FlowParticleInput(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                2f);
            FlowParticleProfile profile = FlowParticleRules.Build(in input);
            FlowColor color = new FlowColor(1, 2, 3);

            field.Spawn(in profile, 0d, 0d, 1, in color);
            FlowAmbientParticle particle = field.Get(0);

            Assert.Equal((byte)115, particle.Alpha);  // round(0.45 × 255) = 115
            Assert.Equal((byte)1, particle.SizePx);   // 1/16 格 = 1 设计像素
        }

        /// <summary>粒子是否在给定像素 bbox（半开区间）内；允许 1/100 像素的浮点误差。</summary>
        private static void AssertWithinPixelBounds(
            FlowAmbientParticle particle,
            double cellX,
            double cellY,
            int x0,
            int x1,
            int y0,
            int y1)
        {
            double minX = cellX + (x0 + 0.5d) / 16d;
            double maxX = cellX + (x1 - 0.5d) / 16d;
            double minY = cellY + (y0 + 0.5d) / 16d;
            double maxY = cellY + (y1 - 0.5d) / 16d;

            Assert.True(
                particle.X >= minX - 1e-4d && particle.X <= maxX + 1e-4d,
                "X 越界：" + particle.X + "，期望 [" + minX + ", " + maxX + "]。");
            Assert.True(
                particle.Y >= minY - 1e-4d && particle.Y <= maxY + 1e-4d,
                "Y 越界：" + particle.Y + "，期望 [" + minY + ", " + maxY + "]。");
        }

        /// <summary>判断粒子出生像素是否落在 bbox 最外 1px 上。</summary>
        private static bool IsOnBBoxEdge(
            FlowAmbientParticle particle,
            double cellX,
            double cellY,
            int x0,
            int x1,
            int y0,
            int y1)
        {
            int pixelX = (int)Math.Round((particle.X - cellX) * 16d - 0.5d);
            int pixelY = (int)Math.Round((particle.Y - cellY) * 16d - 0.5d);

            return pixelX == x0 || pixelX == x1 - 1 || pixelY == y0 || pixelY == y1 - 1;
        }

        /// <summary>推进直到清空，返回步数；超过上限返回 <see cref="int.MaxValue"/>。</summary>
        private static int StepsUntilEmpty(FlowAmbientField field, float step, int maxSteps)
        {
            for (int i = 1; i <= maxSteps; i++)
            {
                field.Advance(step);
                if (field.Count == 0)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }

        /// <summary>
        /// 测试 modifier：只对哨兵速度生效，证明 Register 注册的 modifier 会被 Build 调用，
        /// 同时不改变其它输入的默认值。
        /// </summary>
        private sealed class MarkerModifier : IFlowParticleModifier
        {
            public void Apply(in FlowParticleInput input, ref FlowParticleProfile profile)
            {
                if (input.Speed != MarkerSpeed)
                {
                    return;
                }

                profile.SpawnRate = 1234f;
                profile.SpeedMin = 9f;
                profile.SpeedMax = 10f;
            }
        }
    }
}
