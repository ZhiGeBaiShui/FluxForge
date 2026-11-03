using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using FluxForge.Presentation.Flow.Visual;

namespace FluxForge.Tests
{
    /// <summary>
    /// 图集路线纯逻辑核心（src/Presentation/Flow）的单测：
    /// 成分混合、染色公式、key 生成、索引解析、三级 fallback、Tier-0 阈值、像素取整。
    ///
    /// 这一层零 Unity 依赖，测试项目把 src/Presentation 源码直接编入即可跑。
    /// 任何一条失败都意味着「数据 → 颜色 → key → 坐标 → 画 Image」这条链断了。
    /// </summary>
    public class FlowAtlasTests
    {
        // 枚举全集用数组而不是 Enum.GetValues：不用反射，顺序也与图集生成器一致。
        private static readonly FlowContent[] AllContents =
        {
            FlowContent.Empty, FlowContent.Small, FlowContent.Mid, FlowContent.Large,
        };

        private static readonly FlowState[] AllStates =
        {
            FlowState.Flow, FlowState.Blocked, FlowState.Split,
            FlowState.Merge, FlowState.Collect,
        };

        private static readonly FlowDir[] AllDirs =
        {
            FlowDir.Up, FlowDir.Right, FlowDir.Down, FlowDir.Left,
        };

        // ── key 生成 ────────────────────────────────────────────────

        [Fact]
        public void StaticKeys_All80CombinationsAreUnique()
        {
            // 静态 key 只剩 4 含量 × 5 状态 × 4 方向 = 80（REFACTOR-A §4）。
            // 成分与温度已从 key 里拿掉，任何一个枚举名写错/重复，集合数量都会掉下来。
            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);

            int combinations = AddStaticKeys(keys);

            Assert.Equal(80, combinations);
            Assert.Equal(80, keys.Count);
        }

        [Fact]
        public void StaticKey_HasFrozenChannelOrder()
        {
            FlowVisualState visual = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right);

            Assert.Equal("large|flow|right", visual.ToKey());
        }

        [Fact]
        public void FrameKey_AppendsFrameOnlyForFlow()
        {
            FlowVisualState animated = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right,
                7);
            Assert.Equal("large|flow|right|f7", animated.ToKey());

            FlowVisualState first = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right,
                0);
            Assert.Equal("large|flow|right|f0", first.ToKey());

            FlowVisualState last = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right,
                11);
            Assert.Equal("large|flow|right|f11", last.ToKey());

            // 非 flow 状态即使带了帧号，也不能把 |fN 拼进 key（图集里没有这些 tile）。
            FlowVisualState blocked = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Blocked,
                FlowDir.Right,
                7);
            Assert.Equal("large|blocked|right", blocked.ToKey());

            // 6 参构造 = 静态 tile（Frame 缺省 NoFrame）。
            FlowVisualState staticFlow = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right);
            Assert.Equal("large|flow|right", staticFlow.ToKey());
        }

        [Fact]
        public void Frame_IsClampedByConstructor()
        {
            // 帧越界只在构造时钳一次：之后 key 生成与图集缓存都按 -1..11 处理。
            FlowVisualState before = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right,
                -7);
            Assert.Equal(FlowVisualState.NoFrame, before.Frame);
            Assert.Equal("large|flow|right", before.ToKey());

            FlowVisualState after = new FlowVisualState(
                FlowComposition.PureWater,
                FlowTemp.Normal,
                FlowContent.Large,
                FlowState.Flow,
                FlowDir.Right,
                99);
            Assert.Equal(FlowVisualState.FrameCount - 1, after.Frame);
            Assert.Equal("large|flow|right|f11", after.ToKey());
        }

        [Fact]
        public void CompositionAndTemperature_DoNotAffectKey()
        {
            // A 方案的颜色是运行时乘到灰度蒙版上的，所以成分/温度变化不能换 tile。
            FlowVisualState waterCold = new FlowVisualState(
                new FlowComposition(1f, 0f, 0f, 0f, 0f, 0f),
                FlowTemp.Cold,
                FlowContent.Mid,
                FlowState.Split,
                FlowDir.Left);

            FlowVisualState energyHot = new FlowVisualState(
                new FlowComposition(0f, 0f, 0f, 0f, 0f, 1f),
                FlowTemp.Hot,
                FlowContent.Mid,
                FlowState.Split,
                FlowDir.Left);

            Assert.Equal("mid|split|left", waterCold.ToKey());
            Assert.Equal(waterCold.ToKey(), energyHot.ToKey());
        }

        [Fact]
        public void AllAtlasKeys_StaticPlusFlowFramesAre272Unique()
        {
            // 图集总量：80 静态 + 4 含量 × 4 方向 × 12 帧 = 272（REFACTOR-A §2）。
            Assert.Equal(-1, FlowVisualState.NoFrame);
            Assert.Equal(12, FlowVisualState.FrameCount);

            HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
            AddStaticKeys(keys);

            foreach (FlowContent content in AllContents)
            {
                for (int frame = 0; frame < FlowVisualState.FrameCount; frame++)
                {
                    foreach (FlowDir dir in AllDirs)
                    {
                        FlowVisualState visual = new FlowVisualState(
                            FlowComposition.PureWater,
                            FlowTemp.Normal,
                            content,
                            FlowState.Flow,
                            dir,
                            frame);
                        keys.Add(visual.ToKey());
                    }
                }
            }

            Assert.Equal(272, keys.Count);
        }

        [Fact]
        public void EnumNames_MatchMaskAtlasVocabulary()
        {
            string[] contents = { "empty", "small", "mid", "large" };
            string[] states = { "flow", "blocked", "split", "merge", "collect" };
            string[] dirs = { "up", "right", "down", "left" };

            for (int i = 0; i < contents.Length; i++)
            {
                Assert.Equal(contents[i], FlowVisualState.ContentName(AllContents[i]));
            }

            for (int i = 0; i < states.Length; i++)
            {
                Assert.Equal(states[i], FlowVisualState.StateName(AllStates[i]));
            }

            for (int i = 0; i < dirs.Length; i++)
            {
                Assert.Equal(dirs[i], FlowVisualState.DirName(AllDirs[i]));
            }

            // 越界枚举值不能抛异常：渲染循环里宁可退到查表首项（empty/flow/up），
            // 方向值本身应先经 FlowTierMap.DirFrom 归一化，那里才是 right 兜底。
            Assert.Equal("empty", FlowVisualState.ContentName((FlowContent)9));
            Assert.Equal("flow", FlowVisualState.StateName((FlowState)(-1)));
            Assert.Equal("up", FlowVisualState.DirName((FlowDir)4));
        }

        [Fact]
        public void ToKey_IgnoresRange()
        {
            // 射程不进 key：图集 tile 不含射程点，点由渲染层另画（SPEC §4）。
            FlowVisualState unknown = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Cold, FlowContent.Small,
                FlowState.Flow, FlowDir.Left, FlowVisualState.NoFrame, 0);
            FlowVisualState three = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Cold, FlowContent.Small,
                FlowState.Flow, FlowDir.Left, FlowVisualState.NoFrame, 3);

            Assert.Equal(unknown.ToKey(), three.ToKey());
        }

        // ── 成分混合 ────────────────────────────────────────────────

        [Fact]
        public void Composition_PureWaterBlendsToWaterColor()
        {
            FlowComposition water = FlowComposition.PureWater;

            Assert.False(water.IsEmpty);
            AssertColor(water.Blend(), 0x27, 0xC7, 0xE6);
        }

        [Fact]
        public void Composition_WeightsAreNormalized()
        {
            // 比例相同 → 颜色相同：权重不需要调用方先归一化。
            FlowComposition one = new FlowComposition(1f, 0f, 0f, 0f, 0f, 0f);
            FlowComposition five = new FlowComposition(5f, 0f, 0f, 0f, 0f, 0f);
            FlowColor fiveColor = five.Blend();
            AssertColor(fiveColor, 0x27, 0xC7, 0xE6);
            AssertColor(one.Blend(), fiveColor.R, fiveColor.G, fiveColor.B);

            // 50/50 水 + 毒：逐通道平均。
            // R=(39+155)/2=97；G=(199+92)/2=145.5→146（银行家舍入取偶）；
            // B=(230+255)/2=242.5→242（取偶，与 render_final.py 的 round() 一致）。
            FlowComposition half = new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f);
            Assert.False(half.IsEmpty);
            AssertColor(half.Blend(), 97, 146, 242);

            // 30% 油 + 70% 酸：R=0.3*240+0.7*127=160.9→161，G=0.3*179+0.7*209=200，
            // B=0.3*43+0.7*43=43。
            FlowComposition oilAcid = new FlowComposition(0f, 0f, 0.3f, 0f, 0.7f, 0f);
            AssertColor(oilAcid.Blend(), 161, 200, 43);
        }

        [Fact]
        public void Composition_EmptyOrNaN_FallsBackToWaterColor()
        {
            FlowComposition empty = default(FlowComposition);
            Assert.True(empty.IsEmpty);
            AssertColor(empty.Blend(), 0x27, 0xC7, 0xE6);

            FlowComposition nan = new FlowComposition(1f, float.NaN, 0f, 0f, 0f, 0f);
            Assert.True(nan.IsEmpty);
            AssertColor(nan.Blend(), 0x27, 0xC7, 0xE6);

            // 负权重是脏数据：归一化分母 ≤ 0，也必须退到水色而不是产生 NaN 像素。
            FlowComposition negative = new FlowComposition(-1f, 0f, 0f, 0f, 0f, 0f);
            Assert.False(negative.IsEmpty);
            AssertColor(negative.Blend(), 0x27, 0xC7, 0xE6);
        }

        // ── 染色公式（render_final.edge_color 同式） ─────────────────

        [Fact]
        public void Tint_BodyEqualsCompositionBlend()
        {
            FlowComposition half = new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f);
            AssertColor(FlowTint.Body(in half), 97, 146, 242);

            FlowComposition water = FlowComposition.PureWater;
            AssertColor(FlowTint.Body(in water), 0x27, 0xC7, 0xE6);
        }

        [Fact]
        public void Tint_Edge_UsesShadeThenTemperatureMix()
        {
            FlowComposition water = FlowComposition.PureWater;

            // 本体 #27C7E6 → shade(-0.52) = 每通道 ×0.48：
            // (18.72→19, 95.52→96, 110.4→110)。
            AssertColor(FlowTint.Edge(in water, FlowTemp.Normal), 19, 96, 110);

            // cold: mix(base, #D6ECFF, 0.88) = (190.6→191, 219.2→219, 237.6→238)。
            AssertColor(FlowTint.Edge(in water, FlowTemp.Cold), 191, 219, 238);

            // hot: mix(base, #FF964E, 0.86) = (221.96→222, 142.44→142, 82.48→82)。
            AssertColor(FlowTint.Edge(in water, FlowTemp.Hot), 222, 142, 82);
        }

        [Fact]
        public void Tint_Edge_BlendsFromMixedBody()
        {
            // 50/50 水+毒的本体是 (97,146,242)，压暗后为 (47,70,116)：
            // cold = (193.96→194, 216.08→216, 238.32→238)
            // hot  = (225.88→226, 138.8→139, 83.32→83)
            FlowComposition half = new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f);

            AssertColor(FlowTint.Edge(in half, FlowTemp.Normal), 47, 70, 116);
            AssertColor(FlowTint.Edge(in half, FlowTemp.Cold), 194, 216, 238);
            AssertColor(FlowTint.Edge(in half, FlowTemp.Hot), 226, 139, 83);
        }

        [Fact]
        public void Tint_Accent_TracksComposition()
        {
            // accent = mix(body, white, 0.62)，与旧图 C 角色逐字节一致：
            // 水 #27C7E6 → (39*0.38+158.1, 199*0.38+158.1, 230*0.38+158.1)
            //            = (172.92→173, 233.72→234, 245.5→246)。
            FlowComposition water = FlowComposition.PureWater;
            AssertColor(FlowTint.Accent(in water), 0xAD, 0xEA, 0xF6);

            // 50/50 水+毒的本体是 (97,146,242) → accent (195,214,250)。
            FlowComposition half = new FlowComposition(1f, 1f, 0f, 0f, 0f, 0f);
            AssertColor(FlowTint.Accent(in half), 195, 214, 250);

            // 油/酸必须保留暖亮核：固定 #F2FAFF 会把 B 通道从 174 拉到 255（评审的 Δ≈81）。
            FlowComposition oil = new FlowComposition(0f, 0f, 1f, 0f, 0f, 0f);
            AssertColor(FlowTint.Accent(in oil), 249, 226, 174);

            FlowComposition acid = new FlowComposition(0f, 0f, 0f, 0f, 1f, 0f);
            AssertColor(FlowTint.Accent(in acid), 206, 238, 174);
        }

        // ── 索引解析 ────────────────────────────────────────────────

        [Fact]
        public void Parse_ReadsTilesAndCoordinates()
        {
            const string Json =
                @"{""tiles"":{""large|flow|right"":[16,32],"
                + @"""small|blocked|down"":[0,48],"
                + @"""mid|flow|up|f3"":[32,0]}}";

            FlowAtlasIndex index = FlowAtlasIndex.Parse(Json);

            Assert.Equal(3, index.Count);

            int x;
            int y;
            Assert.True(index.TryGet("large|flow|right", out x, out y));
            Assert.Equal(16, x);
            Assert.Equal(32, y);

            Assert.True(index.TryGet("small|blocked|down", out x, out y));
            Assert.Equal(0, x);
            Assert.Equal(48, y);

            Assert.True(index.TryGet("mid|flow|up|f3", out x, out y));
            Assert.Equal(32, x);
            Assert.Equal(0, y);

            // 旧格式（带 comp/temp）不应再命中新索引。
            Assert.False(index.TryGet("water|normal|large|flow|right", out x, out y));
            Assert.Equal(0, x);
            Assert.Equal(0, y);

            Assert.False(index.TryGet(null, out x, out y));
        }

        [Fact]
        public void Parse_AcceptsUtf8Bom()
        {
            // Unity TextAsset / 编辑器保存的文件可能带 BOM，解析不能因此失败。
            const string Json = @"{""tiles"":{""large|flow|right"":[0,0]}}";

            FlowAtlasIndex index = FlowAtlasIndex.Parse("\uFEFF" + Json);

            Assert.Equal(1, index.Count);
        }

        [Fact]
        public void Parse_InvalidJson_ThrowsWithContext()
        {
            const string Broken = "{\"tiles\":{\"large|flow|right\":[0,0]";

            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse(Broken));

            Assert.Contains("large|flow", exception.Message);
            Assert.NotNull(exception.InnerException);
        }

        [Fact]
        public void Parse_MissingTilesObject_Throws()
        {
            InvalidDataException exception = Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{\"tiles\":[]}"));
            Assert.Contains("tiles", exception.Message);

            exception = Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{}"));
            Assert.Contains("tiles", exception.Message);
        }

        [Fact]
        public void Parse_NullOrEmpty_Throws()
        {
            Assert.Throws<InvalidDataException>(() => FlowAtlasIndex.Parse(null));
            Assert.Throws<InvalidDataException>(() => FlowAtlasIndex.Parse(string.Empty));
        }

        [Fact]
        public void Parse_BadTileCoordinate_Throws()
        {
            Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{\"tiles\":{\"a\":[0]}}"));
            Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{\"tiles\":{\"a\":[0,1,2]}}"));
            Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{\"tiles\":{\"a\":[0.5,1]}}"));
            Assert.Throws<InvalidDataException>(
                () => FlowAtlasIndex.Parse("{\"tiles\":{\"a\":\"0,1\"}}"));
        }

        // ── 三级 fallback ──────────────────────────────────────────

        [Fact]
        public void Fallback_ExactKeyWins()
        {
            FlowAtlasIndex index = IndexWithSingleTile("small|split|right", 48, 64);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Hot, FlowContent.Small, FlowState.Split, FlowDir.Up);

            AssertFallback(state, index, 48, 64);
        }

        [Fact]
        public void Fallback_SecondLevel_DropsStateToFlow()
        {
            // 精确 key（merge）不存在 → 退到同含量/方向的 flow 静态 tile。
            FlowAtlasIndex index = IndexWithSingleTile("small|flow|right", 32, 0);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Hot, FlowContent.Small, FlowState.Merge, FlowDir.Up);

            AssertFallback(state, index, 32, 0);
        }

        [Fact]
        public void Fallback_MissingFrame_DropsToStaticFlowTile()
        {
            // flow 但该帧缺 tile → 第 2 级去掉 |f7，用静态 flow tile 顶上。
            FlowAtlasIndex index = IndexWithSingleTile("large|flow|right", 0, 16);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Normal, FlowContent.Large,
                FlowState.Flow, FlowDir.Right, 7);

            AssertFallback(state, index, 0, 16);
        }

        [Fact]
        public void Fallback_ThirdLevel_DropsContentToLargeKeepingStateAndDir()
        {
            // 第 1、2 级都缺 → 含量退到 large，但状态仍是 blocked（不是退到 flow）。
            FlowAtlasIndex index = IndexWithSingleTile("large|blocked|right", 64, 0);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Hot, FlowContent.Small, FlowState.Blocked, FlowDir.Up);

            AssertFallback(state, index, 64, 0);
        }

        [Fact]
        public void Fallback_ThirdLevel_DoesNotInventFlowState()
        {
            // 只有 large|flow 时，blocked 的请求不能「假装」成 flow：状态是玩家可见的真实信息。
            FlowAtlasIndex index = IndexWithSingleTile("large|flow|right", 80, 0);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Hot, FlowContent.Small, FlowState.Blocked, FlowDir.Up);

            int x;
            int y;
            Assert.False(index.TryGetWithFallback(state, out x, out y));
            Assert.Equal(0, x);
            Assert.Equal(0, y);
        }

        [Fact]
        public void Fallback_ThirdLevel_DropsFlowFrameToo()
        {
            // flow 的缺帧请求：第 3 级是 large|flow（静态），不能拼出 |f5。
            FlowAtlasIndex index = IndexWithSingleTile("large|flow|right", 96, 0);
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Normal, FlowContent.Mid,
                FlowState.Flow, FlowDir.Up, 5);

            AssertFallback(state, index, 96, 0);
        }

        [Fact]
        public void Fallback_NoMatch_ReturnsFalse()
        {
            FlowAtlasIndex index = FlowAtlasIndex.Parse("{\"tiles\":{}}");
            FlowVisualState state = new FlowVisualState(
                FlowComposition.PureWater, FlowTemp.Cold, FlowContent.Mid, FlowState.Collect, FlowDir.Left);

            int x;
            int y;
            Assert.False(index.TryGetWithFallback(state, out x, out y));
            Assert.Equal(0, x);
            Assert.Equal(0, y);
            Assert.Equal(0, index.Count);
        }

        // ── Tier-0 阈值 ────────────────────────────────────────────

        [Fact]
        public void TempFrom_UsesSpecThresholds()
        {
            Assert.Equal(FlowTemp.Cold, FlowTierMap.TempFrom(5f));
            Assert.Equal(FlowTemp.Cold, FlowTierMap.TempFrom(5f - 0.1f));
            Assert.Equal(FlowTemp.Normal, FlowTierMap.TempFrom(5.1f));
            Assert.Equal(FlowTemp.Normal, FlowTierMap.TempFrom(59.9f));
            Assert.Equal(FlowTemp.Hot, FlowTierMap.TempFrom(60f));
            Assert.Equal(FlowTemp.Normal, FlowTierMap.TempFrom(float.NaN));

            // 极端值只做兜底，不应溢出到别的档。
            Assert.Equal(FlowTemp.Cold, FlowTierMap.TempFrom(-40f));
            Assert.Equal(FlowTemp.Hot, FlowTierMap.TempFrom(200f));
        }

        [Fact]
        public void ContentFromWater_UsesSpecThresholds()
        {
            // 只有「真的没有液体」才是空档；1 µL 以上至少是「少」，避免低含量小到看不见。
            Assert.Equal(FlowContent.Empty, FlowTierMap.ContentFromWater(0d));
            Assert.Equal(FlowContent.Empty, FlowTierMap.ContentFromWater(0.5d));
            Assert.Equal(FlowContent.Empty, FlowTierMap.ContentFromWater(-1d));

            Assert.Equal(FlowContent.Small, FlowTierMap.ContentFromWater(1d));
            Assert.Equal(FlowContent.Small, FlowTierMap.ContentFromWater(999d));
            Assert.Equal(FlowContent.Small, FlowTierMap.ContentFromWater(1000d));
            Assert.Equal(FlowContent.Small, FlowTierMap.ContentFromWater(4999999d));

            Assert.Equal(FlowContent.Mid, FlowTierMap.ContentFromWater(5000000d));
            Assert.Equal(FlowContent.Mid, FlowTierMap.ContentFromWater(19999999d));

            Assert.Equal(FlowContent.Large, FlowTierMap.ContentFromWater(20000000d));
            Assert.Equal(FlowContent.Large, FlowTierMap.ContentFromWater(200000000d));

            // NaN 属脏数据，按空档处理，不能凭空画出一大滴。
            Assert.Equal(FlowContent.Empty, FlowTierMap.ContentFromWater(double.NaN));
        }

        [Fact]
        public void TrailLayers_ScaleWithContent()
        {
            // 尾巴长度必须跟体量走：轻流源短尾、重流源长尾，否则含量读数会被拖平。
            Assert.Equal(1, FlowTierMap.TrailLayersFromContent(FlowContent.Empty));
            Assert.Equal(2, FlowTierMap.TrailLayersFromContent(FlowContent.Small));
            Assert.Equal(4, FlowTierMap.TrailLayersFromContent(FlowContent.Mid));
            Assert.Equal(6, FlowTierMap.TrailLayersFromContent(FlowContent.Large));
        }

        [Fact]
        public void StateFrom_BlockedBeatsFlow()
        {
            Assert.Equal(FlowState.Flow, FlowTierMap.StateFrom(false));
            Assert.Equal(FlowState.Blocked, FlowTierMap.StateFrom(true));
        }

        [Fact]
        public void DirFrom_MapsFourDirectionsAndFallsBackToRight()
        {
            Assert.Equal(FlowDir.Up, FlowTierMap.DirFrom(0));
            Assert.Equal(FlowDir.Right, FlowTierMap.DirFrom(1));
            Assert.Equal(FlowDir.Down, FlowTierMap.DirFrom(2));
            Assert.Equal(FlowDir.Left, FlowTierMap.DirFrom(3));

            Assert.Equal(FlowDir.Right, FlowTierMap.DirFrom(-1));
            Assert.Equal(FlowDir.Right, FlowTierMap.DirFrom(4));
            Assert.Equal(FlowDir.Right, FlowTierMap.DirFrom(int.MaxValue));
        }

        [Fact]
        public void DirMath_StepsMatchCoordinateTableAndFallbackToRight()
        {
            // 视觉坐标 Y 向下：Up = (0,-1)、Right = (1,0)、Down = (0,1)、Left = (-1,0)。
            Assert.Equal(0, FlowDirMath.StepX(FlowDir.Up));
            Assert.Equal(-1, FlowDirMath.StepY(FlowDir.Up));

            Assert.Equal(1, FlowDirMath.StepX(FlowDir.Right));
            Assert.Equal(0, FlowDirMath.StepY(FlowDir.Right));

            Assert.Equal(0, FlowDirMath.StepX(FlowDir.Down));
            Assert.Equal(1, FlowDirMath.StepY(FlowDir.Down));

            Assert.Equal(-1, FlowDirMath.StepX(FlowDir.Left));
            Assert.Equal(0, FlowDirMath.StepY(FlowDir.Left));

            // 越界枚举值按 Right 处理（与 FlowTierMap.DirFrom 的兜底一致）。
            Assert.Equal(1, FlowDirMath.StepX((FlowDir)99));
            Assert.Equal(0, FlowDirMath.StepY((FlowDir)99));

            // 反方向就是取反，残影/粒子后偏共用同一张表。
            Assert.Equal(-1, FlowDirMath.BackwardX(FlowDir.Right));
            Assert.Equal(1, FlowDirMath.BackwardY(FlowDir.Up));
            Assert.Equal(0, FlowDirMath.BackwardX(FlowDir.Down));
            Assert.Equal(-1, FlowDirMath.BackwardY(FlowDir.Down));
        }

        // ── 像素数学 ────────────────────────────────────────────────

        [Fact]
        public void CellToLocal_OffsetsWithinCell()
        {
            Assert.Equal(16, FlowPixelMath.CellPixels);
            Assert.Equal(0, FlowPixelMath.CellToLocal(0, 0));
            Assert.Equal(53, FlowPixelMath.CellToLocal(3, 5));
            Assert.Equal(256, FlowPixelMath.CellToLocal(16, 0));

            // 负格坐标是合法输入（面板外的裁剪由渲染层处理）。
            Assert.Equal(-1, FlowPixelMath.CellToLocal(-1, 15));
        }

        [Fact]
        public void Snap_RoundsToPixelGrid()
        {
            // 7.0625 = 113/16：UGUI ppu=113 时「1 屏幕像素」对应的局部单位。
            const float Scale = 7.0625f;
            float onePixel = 1f / Scale;

            // 1/7.0625 乘回去正好 1 个像素 → 取整后原样返回。
            Assert.Equal(onePixel, FlowPixelMath.Snap(onePixel, Scale), 6);

            // 0.04 × 7.0625 ≈ 0.28 → 吸到 0。
            Assert.Equal(0f, FlowPixelMath.Snap(0.04f, Scale), 6);

            // 0.15 × 7.0625 ≈ 1.06 → 吸到 1 个像素。
            Assert.Equal(onePixel, FlowPixelMath.Snap(0.15f, Scale), 6);

            Assert.Equal(2f, FlowPixelMath.Snap(1.999f, 1f), 6);
            Assert.Equal(-1f, FlowPixelMath.Snap(-1.4f, 1f), 6);
        }

        [Fact]
        public void Snap_ScaleNotReady_ReturnsInputUnchanged()
        {
            // 屏幕缩放未就绪时直接返回，避免 0/NaN 缩放把坐标变成 Infinity/NaN。
            Assert.Equal(1.25f, FlowPixelMath.Snap(1.25f, 0f), 6);
            Assert.Equal(1.25f, FlowPixelMath.Snap(1.25f, -2f), 6);
            Assert.Equal(1.25f, FlowPixelMath.Snap(1.25f, float.NaN), 6);
        }

        // ── 辅助 ───────────────────────────────────────────────────

        /// <summary>把 80 个静态组合的 key 塞进集合，返回生成的组合数。</summary>
        private static int AddStaticKeys(HashSet<string> keys)
        {
            int combinations = 0;

            foreach (FlowContent content in AllContents)
            {
                foreach (FlowState state in AllStates)
                {
                    foreach (FlowDir dir in AllDirs)
                    {
                        FlowVisualState visual = new FlowVisualState(
                            FlowComposition.PureWater, FlowTemp.Normal, content, state, dir);
                        keys.Add(visual.ToKey());
                        combinations++;
                    }
                }
            }

            return combinations;
        }

        /// <summary>用一条 tile 造最小索引，便于单独验证 fallback 的某一级。</summary>
        private static FlowAtlasIndex IndexWithSingleTile(string key, int x, int y)
        {
            string json = "{\"tiles\":{\"" + key + "\":[" + x + "," + y + "]}}";
            return FlowAtlasIndex.Parse(json);
        }

        private static void AssertFallback(FlowVisualState state, FlowAtlasIndex index, int expectedX, int expectedY)
        {
            int x;
            int y;

            Assert.True(
                index.TryGetWithFallback(state, out x, out y),
                "fallback 应命中：" + state.ToKey());
            Assert.Equal(expectedX, x);
            Assert.Equal(expectedY, y);
        }

        private static void AssertColor(FlowColor actual, byte r, byte g, byte b)
        {
            Assert.Equal(r, actual.R);
            Assert.Equal(g, actual.G);
            Assert.Equal(b, actual.B);
        }
    }
}
