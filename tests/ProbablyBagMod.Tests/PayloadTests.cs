using Xunit;
using ProbablyBagMod;

namespace ProbablyBagMod.Tests
{
    public class PayloadTests
    {
        private static LiquidPayload MakeLiquid(double moisture, double alcohol, double sugar, double impurity)
        {
            return new LiquidPayload
            {
                Moisture = moisture,
                Alcohol = alcohol,
                Sugar = sugar,
                Impurity = impurity,
            };
        }

        [Fact]
        public void Construct_WithAllRules_ShouldSucceed()
        {
            var p = MakeLiquid(60, 12, 0, 2);
            Assert.Equal(60, p.Moisture);
        }

        [Fact]
        public void Split_DividesEveryField_AndKeepsTotal()
        {
            var p = MakeLiquid(100, 20, 8, 4);

            IReadOnlyList<IFlowPayload> parts = p.Split(2);

            Assert.Equal(2, parts.Count);
            var a = (LiquidPayload)parts[0];
            var b = (LiquidPayload)parts[1];

            Assert.Equal(50, a.Moisture);
            Assert.Equal(50, b.Moisture);
            Assert.Equal(10, a.Alcohol);
            Assert.Equal(10, b.Alcohol);

            // 总量守恒：分流后总和必须等于分流前
            Assert.Equal(100, a.Moisture + b.Moisture);
            Assert.Equal(20, a.Alcohol + b.Alcohol);
            Assert.Equal(8, a.Sugar + b.Sugar);
            Assert.Equal(4, a.Impurity + b.Impurity);
        }

        [Fact]
        public void Split_IsPureFunction_OriginalUnchanged()
        {
            var p = MakeLiquid(100, 20, 8, 4);

            p.Split(2);

            // 原对象必须完全不变（纯函数）
            Assert.Equal(100, p.Moisture);
            Assert.Equal(20, p.Alcohol);
            Assert.Equal(8, p.Sugar);
            Assert.Equal(4, p.Impurity);
        }

        [Fact]
        public void Merge_AddsEveryField()
        {
            var a = MakeLiquid(60, 12, 0, 2);
            var b = MakeLiquid(70, 0, 25, 1);

            var merged = (LiquidPayload)a.Merge(b);

            Assert.Equal(130, merged.Moisture);
            Assert.Equal(12, merged.Alcohol);
            Assert.Equal(25, merged.Sugar);
            Assert.Equal(3, merged.Impurity);
        }

        [Fact]
        public void Merge_IsPureFunction_ArgumentsUnchanged()
        {
            var a = MakeLiquid(60, 12, 0, 2);
            var b = MakeLiquid(70, 0, 25, 1);

            a.Merge(b);

            Assert.Equal(60, a.Moisture);
            Assert.Equal(70, b.Moisture);
        }

        [Fact]
        public void Merge_DifferentType_ShouldThrow()
        {
            var a = MakeLiquid(60, 0, 0, 0);
            IFlowPayload b = new OtherPayload();

            Assert.Throws<ArgumentException>(() => a.Merge(b));
        }

        [Fact]
        public void Split_ZeroOrNegative_ShouldThrow()
        {
            var p = MakeLiquid(10, 0, 0, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => p.Split(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => p.Split(-1));
        }

        [Fact]
        public void FieldWithoutRule_ShouldThrowAndNameIt()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new BrokenPayload());

            Assert.Contains("BrokenPayload.Sugar", ex.Message);
        }

        [Fact]
        public void RuleForMissingField_ShouldThrowAndNameIt()
        {
            var ex = Assert.Throws<InvalidOperationException>(() => new GhostRulePayload());

            Assert.Contains("NoSuchField", ex.Message);
        }

        private sealed class OtherPayload : FlowPayloadBase
        {
            private static readonly PropertyRule[] RulesImpl = new[]
            {
                new PropertyRule(nameof(Value), SplitRule.Divide, MergeRule.Add),
            };

            public double Value;

            protected override IReadOnlyList<PropertyRule> Rules => RulesImpl;
        }

        private sealed class BrokenPayload : FlowPayloadBase
        {
            private static readonly PropertyRule[] RulesImpl = new[]
            {
                new PropertyRule(nameof(Moisture), SplitRule.Divide, MergeRule.Add),
            };

            public double Moisture;
            public double Sugar;

            protected override IReadOnlyList<PropertyRule> Rules => RulesImpl;
        }

        private sealed class GhostRulePayload : FlowPayloadBase
        {
            private static readonly PropertyRule[] RulesImpl = new[]
            {
                new PropertyRule(nameof(Moisture), SplitRule.Divide, MergeRule.Add),
                new PropertyRule("NoSuchField", SplitRule.Divide, MergeRule.Add),
            };

            public double Moisture;

            protected override IReadOnlyList<PropertyRule> Rules => RulesImpl;
        }
    }
}
