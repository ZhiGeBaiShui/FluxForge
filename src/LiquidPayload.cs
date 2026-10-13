using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 液体属性（示例实现）。
    ///
    /// 所有液体共用同一结构，不同液体只是字段取值不同：
    ///   酒    = 水分 60 / 酒精 12 / 糖分 0 / 杂质 2
    ///   奶茶  = 水分 70 / 酒精 0 / 糖分 25 / 杂质 1
    ///   化学液 = 水分 40 / 酒精 0 / 糖分 0 / 杂质 15
    ///
    /// 「价值」不在此列——它由外部 <c>LiquidValues.Calculate(payload)</c> 实时算出，不存储。
    ///
    /// ⚠️ <see cref="Rules"/> 必须是静态数据：基类构造函数会读取它，
    /// 那时子类的实例字段尚未初始化。
    /// </summary>
    public sealed class LiquidPayload : FlowPayloadBase
    {
        /// <summary>水分含量。</summary>
        public double Moisture;

        /// <summary>酒精含量。</summary>
        public double Alcohol;

        /// <summary>糖分含量。</summary>
        public double Sugar;

        /// <summary>杂质含量。</summary>
        public double Impurity;

        private static readonly PropertyRule[] RuleTable = new[]
        {
            new PropertyRule(nameof(Moisture), SplitRule.Divide, MergeRule.Add),
            new PropertyRule(nameof(Alcohol),  SplitRule.Divide, MergeRule.Add),
            new PropertyRule(nameof(Sugar),    SplitRule.Divide, MergeRule.Add),
            new PropertyRule(nameof(Impurity), SplitRule.Divide, MergeRule.Add),
        };

        protected override IReadOnlyList<PropertyRule> Rules => RuleTable;
    }
}
