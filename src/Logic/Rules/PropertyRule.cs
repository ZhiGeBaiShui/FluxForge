namespace ProbablyBagMod
{
    /// <summary>
    /// 分流时该字段如何处理。
    /// </summary>
    public enum SplitRule
    {
        /// <summary>等分：每份得到原值的 1/count。</summary>
        Divide,

        /// <summary>不变：每份保留原值（例：温度不随分流改变）。</summary>
        Keep,
    }

    /// <summary>汇聚时该字段如何处理。</summary>
    public enum MergeRule
    {
        /// <summary>相加：合并结果为各份之和（例：水分）。</summary>
        Add,

        /// <summary>
        /// 加权平均：按各份的「总物质含量」加权
        /// （例：温度，物质多的那份说话更响）。
        /// </summary>
        WeightedAverage,
    }

    /// <summary>
    /// 一条属性规则：描述某个字段在分流 / 汇聚时如何处理。
    ///
    /// 规则必须逐字段显式声明——<see cref="FlowProperties"/> 的静态构造函数会校验
    /// 「字段 ↔ 规则」一一对应，漏声明或指向不存在的字段都会直接抛异常。
    /// </summary>
    public sealed class PropertyRule
    {
        /// <summary>字段名。用 <c>nameof(字段)</c> 传入，避免拼写错误后被漏检。</summary>
        public string FieldName { get; }

        public SplitRule Split { get; }

        public MergeRule Merge { get; }

        public PropertyRule(string fieldName, SplitRule split, MergeRule merge)
        {
            FieldName = fieldName;
            Split = split;
            Merge = merge;
        }
    }
}
