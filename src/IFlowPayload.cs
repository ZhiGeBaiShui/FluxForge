namespace ProbablyBagMod
{
    /// <summary>
    /// 流源载荷：流源携带的「数据」（性质 / 成分）。
    ///
    /// 框架完全不解释它的内容，只负责搬运。由外部（反应层）定义实现。
    /// 框架只依赖两个能力：
    ///   <see cref="Clone"/> —— 分流时复制载荷
    ///   <see cref="Merge"/> —— 汇聚时合并两个载荷
    ///
    /// 因此装置可以处理分流与汇聚，却完全不需要知道载荷是什么。
    /// </summary>
    public interface IFlowPayload
    {
        /// <summary>复制一份载荷（分流用）。</summary>
        IFlowPayload Clone();

        /// <summary>与另一份载荷合并，返回合并产物（汇聚用）。</summary>
        IFlowPayload Merge(IFlowPayload other);
    }
}
