using Xunit;
using ProbablyBagMod;

namespace ProbablyBagMod.Tests
{
    public class FlowPropertiesTests
    {
        private static FlowProperties Make(double water, double temperature)
        {
            return new FlowProperties(water, temperature);
        }

        [Fact]
        public void Ctor_SetsFields()
        {
            FlowProperties p = Make(100d, 20d);

            Assert.Equal(100d, p.Water);
            Assert.Equal(20d, p.Temperature);
        }

        [Fact]
        public void Split_DividesWater_ButKeepsTemperature()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(2);

            Assert.Equal(2, parts.Count);
            foreach (FlowProperties part in parts)
            {
                Assert.Equal(50d, part.Water);    // 平分
                Assert.Equal(50d, part.Temperature); // 温度不变
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
                sum += part.Water;
            }
            Assert.Equal(100d, sum);   // 总量守恒
        }

        [Fact]
        public void Split_ProducedPartsAreIndependentInstances()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(3);

            Assert.NotSame(parts[0], parts[1]);
            parts[0].Water = 999d;
            Assert.Equal(100d / 3d, parts[1].Water);   // 改一份不影响另一份
        }

        [Fact]
        public void Split_IsPure_OriginalUnchanged()
        {
            FlowProperties p = Make(100d, 50d);

            p.Split(3);

            Assert.Equal(100d, p.Water);
            Assert.Equal(50d, p.Temperature);
        }

        [Fact]
        public void Merge_AddsWater_WeightedAverageTemperature()
        {
            // A: 水量60 温度20   B: 水量40 温度80
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            List<FlowProperties> rest = new List<FlowProperties> { b };
            FlowProperties merged = a.Merge(rest);

            Assert.Equal(100d, merged.Water);
            // (20*60 + 80*40) / 100 = 44
            Assert.Equal(44d, merged.Temperature);
        }

        [Fact]
        public void Merge_IsPure_ArgumentsUnchanged()
        {
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            a.Merge(new List<FlowProperties> { b });

            Assert.Equal(60d, a.Water);
            Assert.Equal(20d, a.Temperature);
            Assert.Equal(40d, b.Water);
            Assert.Equal(80d, b.Temperature);
        }

        [Fact]
        public void Merge_ThreeParts()
        {
            FlowProperties a = Make(50d, 10d);
            FlowProperties b = Make(50d, 20d);
            FlowProperties c = Make(100d, 90d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b, c });

            Assert.Equal(200d, merged.Water);
            // (10*50 + 20*50 + 90*100) / 200 = 52.5
            Assert.Equal(52.5d, merged.Temperature);
        }

        [Fact]
        public void Merge_ZeroMassDenominator_KeepsFirstTemperature()
        {
            // 两份水量都是 0 → 加权平均没有意义，保留第一份的温度
            FlowProperties a = Make(0d, 20d);
            FlowProperties b = Make(0d, 80d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(0d, merged.Water);
            Assert.Equal(20d, merged.Temperature);
        }

        [Fact]
        public void Merge_EmptyList_ReturnsCopyOfThis()
        {
            FlowProperties a = Make(70d, 30d);

            FlowProperties merged = a.Merge(new List<FlowProperties>());

            Assert.Equal(70d, merged.Water);
            Assert.Equal(30d, merged.Temperature);
            Assert.NotSame(a, merged);   // 是新对象，不改原对象
        }

        [Fact]
        public void Merge_WeightsUseLiveFieldValues_NotStaleState()
        {
            // 入参刚被直接改过字段：Merge 必须按当前真实值加权
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);
            b.Water = 200d;      // 直接改字段，没有中间状态可刷

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(260d, merged.Water);
            // 按真实水量加权：(20*60 + 80*200) / 260 = 66.15...
            Assert.Equal((20d * 60d + 80d * 200d) / 260d, merged.Temperature, 6);
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

            Assert.Throws<ArgumentNullException>(() => p.Merge(null));
        }
    }
}
