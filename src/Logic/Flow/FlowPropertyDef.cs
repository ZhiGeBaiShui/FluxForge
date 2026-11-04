namespace FluxForge
{
    /// <summary>流源属性的数据种类。</summary>
    public enum FlowPropertyKind
    {
        /// <summary>液体成分：计入物质总量，随分流/汇聚分合。</summary>
        Liquid,

        /// <summary>标量属性：不是物质，不计入物质总量（例：温度）。</summary>
        Scalar,
    }

    /// <summary>分流时该属性如何处理。</summary>
    public enum SplitRule
    {
        /// <summary>等分：每份得到原值的 1/count。</summary>
        Divide,

        /// <summary>不变：每份保留原值（例：温度不随分流改变）。</summary>
        Keep,
    }

    /// <summary>汇聚时该属性如何处理。</summary>
    public enum MergeRule
    {
        /// <summary>相加：合并结果为各份之和（例：液体成分）。</summary>
        Add,

        /// <summary>
        /// 加权平均：按各份的「总物质含量」加权
        /// （例：温度，物质多的那份说话更响）。
        /// </summary>
        WeightedAverage,
    }

    /// <summary>
    /// 属性注册表里的槽位键。
    /// <para>
    /// <see cref="Index"/> 是 1 基槽位；默认值 Index=0 不对应任何属性，
    /// 用它读写 <see cref="FlowProperties"/> 会自然越界。
    /// </para>
    /// </summary>
    public readonly struct FlowPropertyKey
    {
        /// <summary>注册表中的 1 基槽位。</summary>
        public readonly int Index;

        public FlowPropertyKey(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// 一条流源属性定义：身份、数据种类、分合规则与槽位。
    /// 只由 <see cref="FlowPropertyRegistry"/> 构造。
    /// </summary>
    public sealed class FlowPropertyDef
    {
        /// <summary>稳定标识（例：water / container_temp）。</summary>
        public string Id { get; }

        /// <summary>数据种类。</summary>
        public FlowPropertyKind Kind { get; }

        /// <summary>分流规则。</summary>
        public SplitRule Split { get; }

        /// <summary>汇聚规则。</summary>
        public MergeRule Merge { get; }

        /// <summary>读写 <see cref="FlowProperties"/> 的槽位键。</summary>
        public FlowPropertyKey Key { get; }

        public FlowPropertyDef(
            string id, FlowPropertyKind kind, SplitRule split, MergeRule merge, int oneBasedIndex)
        {
            Id = id;
            Kind = kind;
            Split = split;
            Merge = merge;
            Key = new FlowPropertyKey(oneBasedIndex);
        }
    }
}
