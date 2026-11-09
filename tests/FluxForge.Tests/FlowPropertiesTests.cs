using Xunit;
using FluxForge;

namespace FluxForge.Tests
{
    public class FlowPropertiesTests
    {
        private static FlowProperties Make(double water, double temperature)
        {
            return TestFlow.Make(water, temperature);
        }

        [Fact]
        public void Make_SetsWaterAndTemperature()
        {
            FlowProperties p = Make(100d, 20d);

            Assert.Equal(100d, p.Get(FlowProps.Water));
            Assert.Equal(20d, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void DefaultCtor_IsAllZero()
        {
            FlowProperties p = new FlowProperties();

            Assert.Equal(0d, p.SubstanceTotal);
            Assert.Equal(0d, p.Get(FlowProps.Water));
            Assert.Equal(0d, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Split_DividesWater_ButKeepsTemperature()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(2);

            Assert.Equal(2, parts.Count);
            foreach (FlowProperties part in parts)
            {
                Assert.Equal(50d, part.Get(FlowProps.Water));    // 平分
                Assert.Equal(50d, part.Get(FlowProps.Temperature)); // 温度不变
            }
        }

        [Fact]
        public void Split_KeepsTotalAmountConsistent()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(2);

            double sum = 0d;
            foreach (FlowProperties part in parts)
            {
                sum += part.Get(FlowProps.Water);
            }
            Assert.Equal(100d, sum);   // 总量守恒
        }

        [Fact]
        public void Split_ProducedPartsAreIndependentInstances()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(3);

            Assert.NotSame(parts[0], parts[1]);
            parts[0].Set(FlowProps.Water, 999d);
            Assert.Equal(100d / 3d, parts[1].Get(FlowProps.Water));   // 改一份不影响另一份
        }

        [Fact]
        public void Split_IsPure_OriginalUnchanged()
        {
            FlowProperties p = Make(100d, 50d);

            p.Split(3);

            Assert.Equal(100d, p.Get(FlowProps.Water));
            Assert.Equal(50d, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Split_MixedLiquids_DividesEveryLiquidAndKeepsTemperature()
        {
            // 水 90 + 油 30，40℃：每一份都应是水 30 + 油 10，温度保持 40
            FlowProperties p = Make(90d, 40d);
            p.Set(FlowProps.Fat, 30d);

            List<FlowProperties> parts = p.Split(3);

            Assert.Equal(3, parts.Count);
            foreach (FlowProperties part in parts)
            {
                Assert.Equal(30d, part.Get(FlowProps.Water));
                Assert.Equal(10d, part.Get(FlowProps.Fat));
                Assert.Equal(40d, part.Get(FlowProps.Temperature));
            }

            // 源对象不变
            Assert.Equal(90d, p.Get(FlowProps.Water));
            Assert.Equal(30d, p.Get(FlowProps.Fat));
            Assert.Equal(40d, p.Get(FlowProps.Temperature));

            // 三份互不影响：改一份的非首个槽（油）不波及其它两份
            parts[0].Set(FlowProps.Fat, 999d);
            Assert.Equal(10d, parts[1].Get(FlowProps.Fat));
            Assert.Equal(10d, parts[2].Get(FlowProps.Fat));
            Assert.Equal(30d, parts[1].Get(FlowProps.Water));
            Assert.Equal(30d, parts[2].Get(FlowProps.Water));
        }

        [Fact]
        public void Merge_AddsWater_WeightedAverageTemperature()
        {
            // A: 水量60 温度20   B: 水量40 温度80
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            List<FlowProperties> rest = new List<FlowProperties> { b };
            FlowProperties merged = a.Merge(rest);

            Assert.Equal(100d, merged.Get(FlowProps.Water));
            // (20*60 + 80*40) / 100 = 44
            Assert.Equal(44d, merged.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Merge_SingleOther_MatchesListMerge()
        {
            // 单份重载供「收到一个就并一个」的调用方使用，语义必须与列表版一致
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            FlowProperties merged = a.Merge(b);

            Assert.Equal(100d, merged.Get(FlowProps.Water));
            Assert.Equal(44d, merged.Get(FlowProps.Temperature));
            // 纯函数：入参不变
            Assert.Equal(60d, a.Get(FlowProps.Water));
            Assert.Equal(40d, b.Get(FlowProps.Water));
        }

        [Fact]
        public void MergeFrom_MatchesMerge_AndMutatesThis()
        {
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);
            FlowProperties expected = a.Merge(b);

            a.MergeFrom(b);

            Assert.Equal(expected.Get(FlowProps.Water), a.Get(FlowProps.Water));
            Assert.Equal(expected.Get(FlowProps.Temperature), a.Get(FlowProps.Temperature));
            Assert.Equal(40d, b.Get(FlowProps.Water));
            Assert.Equal(80d, b.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Merge_IsPure_ArgumentsUnchanged()
        {
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            a.Merge(new List<FlowProperties> { b });

            Assert.Equal(60d, a.Get(FlowProps.Water));
            Assert.Equal(20d, a.Get(FlowProps.Temperature));
            Assert.Equal(40d, b.Get(FlowProps.Water));
            Assert.Equal(80d, b.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Merge_ThreeParts()
        {
            FlowProperties a = Make(50d, 10d);
            FlowProperties b = Make(50d, 20d);
            FlowProperties c = Make(100d, 90d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b, c });

            Assert.Equal(200d, merged.Get(FlowProps.Water));
            // (10*50 + 20*50 + 90*100) / 200 = 52.5
            Assert.Equal(52.5d, merged.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Merge_MixedLiquids_AddsEachLiquidAndWeightsTemperatureByMass()
        {
            // A: 水40 + 油60 @20℃（总物质100）；B: 水100 @80℃（总物质100）
            FlowProperties a = Make(40d, 20d);
            a.Set(FlowProps.Fat, 60d);
            FlowProperties b = Make(100d, 80d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(140d, merged.Get(FlowProps.Water));
            Assert.Equal(60d, merged.Get(FlowProps.Fat));
            // (20*100 + 80*100) / 200 = 50
            Assert.Equal(50d, merged.Get(FlowProps.Temperature));
            Assert.Equal(200d, merged.SubstanceTotal);
        }

        [Fact]
        public void Merge_ResultIsDeepCopied_IndependentFromInputs()
        {
            FlowProperties a = Make(40d, 20d);
            a.Set(FlowProps.Fat, 60d);
            FlowProperties b = Make(100d, 80d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.NotSame(a, merged);
            Assert.NotSame(b, merged);

            // 改结果不影响入参
            merged.Set(FlowProps.Water, 1d);
            merged.Set(FlowProps.Fat, 2d);
            Assert.Equal(40d, a.Get(FlowProps.Water));
            Assert.Equal(60d, a.Get(FlowProps.Fat));
            Assert.Equal(100d, b.Get(FlowProps.Water));

            // 改入参也不影响结果
            b.Set(FlowProps.Water, 7d);
            Assert.Equal(1d, merged.Get(FlowProps.Water));
        }

        [Fact]
        public void Merge_ZeroMassDenominator_KeepsFirstTemperature()
        {
            // 两份水量都是 0 → 加权平均没有意义，保留第一份的温度
            FlowProperties a = Make(0d, 20d);
            FlowProperties b = Make(0d, 80d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(0d, merged.Get(FlowProps.Water));
            Assert.Equal(20d, merged.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Merge_EmptyList_ReturnsCopyOfThis()
        {
            FlowProperties a = Make(70d, 30d);

            FlowProperties merged = a.Merge(new List<FlowProperties>());

            Assert.Equal(70d, merged.Get(FlowProps.Water));
            Assert.Equal(30d, merged.Get(FlowProps.Temperature));
            Assert.NotSame(a, merged);   // 是新对象，不改原对象
        }

        [Fact]
        public void Merge_WeightsUseLiveValues_NotStaleState()
        {
            // 入参刚被直接改过：Merge 必须按当前真实值加权
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);
            b.Set(FlowProps.Water, 200d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(260d, merged.Get(FlowProps.Water));
            // 按真实水量加权：(20*60 + 80*200) / 260 = 66.15...
            Assert.Equal((20d * 60d + 80d * 200d) / 260d, merged.Get(FlowProps.Temperature), 6);
        }

        [Fact]
        public void SubstanceTotal_SumsLiquidsAndForeign_ExcludesTemperature()
        {
            FlowProperties p = Make(10d, 100d);
            p.Set(FlowProps.Fat, 5d);
            p.Set(FlowProps.Alcohol, 2d);
            p.Set(FlowProps.Foreign, 4d);

            Assert.Equal(21d, p.SubstanceTotal);
        }

        [Fact]
        public void Foreign_SplitsAndMergesLikeLiquid()
        {
            FlowProperties p = Make(100d, 40d);
            p.Set(FlowProps.Foreign, 30d);

            List<FlowProperties> parts = p.Split(2);
            Assert.Equal(15d, parts[0].Get(FlowProps.Foreign));
            Assert.Equal(15d, parts[1].Get(FlowProps.Foreign));

            FlowProperties merged = parts[0].Merge(parts[1]);
            Assert.Equal(30d, merged.Get(FlowProps.Foreign));
        }

        [Fact]
        public void Clone_IsDeepCopy()
        {
            FlowProperties p = Make(10d, 20d);
            p.Set(FlowProps.Fat, 3d);

            FlowProperties clone = p.Clone();
            clone.Set(FlowProps.Water, 99d);
            clone.Set(FlowProps.Fat, 88d);
            clone.Set(FlowProps.Temperature, -1d);

            Assert.Equal(10d, p.Get(FlowProps.Water));
            Assert.Equal(3d, p.Get(FlowProps.Fat));
            Assert.Equal(20d, p.Get(FlowProps.Temperature));
        }

        [Fact]
        public void Get_Set_DefaultKey_ThrowsIndexOutOfRange()
        {
            FlowProperties p = Make(100d, 20d);
            FlowPropertyKey unset = default;

            Assert.Throws<IndexOutOfRangeException>(() => p.Get(unset));
            Assert.Throws<IndexOutOfRangeException>(() => p.Set(unset, 1d));
        }

        [Fact]
        public void Split_InvalidCount_Throws()
        {
            FlowProperties p = Make(100d, 20d);

            Assert.Throws<ArgumentOutOfRangeException>(() => p.Split(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.Split(-2));
        }

        [Fact]
        public void Merge_NullArgument_Throws()
        {
            FlowProperties p = Make(100d, 20d);

            Assert.Throws<ArgumentNullException>(() => p.Merge((List<FlowProperties>)null));
            Assert.Throws<ArgumentNullException>(() => p.Merge((FlowProperties)null));
        }
    }
}
