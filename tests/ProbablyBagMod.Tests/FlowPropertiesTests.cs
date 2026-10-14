using Xunit;
using ProbablyBagMod;

namespace ProbablyBagMod.Tests
{
    public class FlowPropertiesTests
    {
        private static FlowProperties Make(double moisture, double temperature)
        {
            return new FlowProperties(moisture, temperature);
        }

        [Fact]
        public void Ctor_SetsFieldsAndTotalMass()
        {
            FlowProperties p = Make(100d, 20d);

            Assert.Equal(100d, p.Moisture);
            Assert.Equal(20d, p.Temperature);
            Assert.Equal(100d, p.TotalMass);   // 只有 Moisture 是 Add 字段
        }

        [Fact]
        public void Split_DividesMoisture_ButKeepsTemperature()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(2);

            Assert.Equal(2, parts.Count);
            foreach (FlowProperties part in parts)
            {
                Assert.Equal(50d, part.Moisture);    // 平分
                Assert.Equal(50d, part.Temperature); // 温度不变
            }
        }

        [Fact]
        public void Split_KeepsTotalMassConsistent()
        {
            FlowProperties p = Make(100d, 50d);

            List<FlowProperties> parts = p.Split(2);

            double sum = 0d;
            foreach (FlowProperties part in parts)
            {
                sum += part.Moisture;
                Assert.Equal(50d, part.TotalMass);   // 每份的 TotalMass 与自身水分一致
            }
            Assert.Equal(100d, sum);                  // 总量守恒
        }

        [Fact]
        public void Split_IsPure_OriginalUnchanged()
        {
            FlowProperties p = Make(100d, 50d);

            p.Split(3);

            Assert.Equal(100d, p.Moisture);
            Assert.Equal(50d, p.Temperature);
            Assert.Equal(100d, p.TotalMass);
        }

        [Fact]
        public void Merge_AddsMoisture_WeightedAverageTemperature()
        {
            // A: 水分60 温度20   B: 水分40 温度80
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            var rest = new List<FlowProperties> { b };
            FlowProperties merged = a.Merge(rest);

            Assert.Equal(100d, merged.Moisture);
            // (20*60 + 80*40) / 100 = 44
            Assert.Equal(44d, merged.Temperature);
            Assert.Equal(100d, merged.TotalMass);
        }

        [Fact]
        public void Merge_IsPure_ArgumentsUnchanged()
        {
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);

            a.Merge(new List<FlowProperties> { b });

            Assert.Equal(60d, a.Moisture);
            Assert.Equal(20d, a.Temperature);
            Assert.Equal(40d, b.Moisture);
            Assert.Equal(80d, b.Temperature);
        }

        [Fact]
        public void Merge_ThreeParts()
        {
            FlowProperties a = Make(50d, 10d);
            FlowProperties b = Make(50d, 20d);
            FlowProperties c = Make(100d, 90d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b, c });

            Assert.Equal(200d, merged.Moisture);
            // (10*50 + 20*50 + 90*100) / 200 = 52.5
            Assert.Equal(52.5d, merged.Temperature);
        }

        [Fact]
        public void Merge_ZeroMassDenominator_KeepsFirstTemperature()
        {
            // 两份水分都是 0 → 加权平均没有意义，保留第一份的温度
            FlowProperties a = Make(0d, 20d);
            FlowProperties b = Make(0d, 80d);

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(0d, merged.Moisture);
            Assert.Equal(20d, merged.Temperature);
        }

        [Fact]
        public void Merge_EmptyList_ReturnsCopyOfThis()
        {
            FlowProperties a = Make(70d, 30d);

            FlowProperties merged = a.Merge(new List<FlowProperties>());

            Assert.Equal(70d, merged.Moisture);
            Assert.Equal(30d, merged.Temperature);
            Assert.Equal(70d, merged.TotalMass);
        }

        [Fact]
        public void Merge_RecalculateAfterDirectFieldChange()
        {
            FlowProperties p = Make(100d, 20d);

            p.Moisture = 40d;
            p.Recalculate();

            Assert.Equal(40d, p.TotalMass);
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

        [Fact]
        public void Merge_DoesNotTrustStaleTotalMassOfInput()
        {
            // 入参的 TotalMass 过期（水分改了没刷），Merge 仍应算对
            FlowProperties a = Make(60d, 20d);
            FlowProperties b = Make(40d, 80d);
            b.Moisture = 200d;      // 直接改字段，不调 Recalculate → b.TotalMass 仍是 40

            FlowProperties merged = a.Merge(new List<FlowProperties> { b });

            Assert.Equal(260d, merged.Moisture);
            // 按真实水分加权：(20*60 + 80*200) / 260 = 66.15...
            Assert.Equal((20d * 60d + 80d * 200d) / 260d, merged.Temperature, 6);
        }
    }
}
