using System.Collections.Generic;

namespace ProbablyBagMod
{
    /// <summary>
    /// 流源载荷：流源携带的「数据」（性质 / 成分）。
    ///
    /// 框架完全不解释它的内容，只负责搬运。由外部（反应层）定义实现。
    /// 框架只依赖两个能力：
    ///   <see cref="Split"/> —— 分流时把含量平分成多份（**总量守恒，不会增值**）
    ///   <see cref="Merge"/> —— 汇聚时合成一份
    ///
    /// 两个方法都必须是**纯函数**：不得修改本对象与入参。
    /// 否则装置 buffer 里寄存的载荷会被污染，状态与价值判断也会拿到过期数据。
    ///
    /// 具体分合规则由各属性类自行决定（水分可平分、温度不平分…），框架不预设。
    /// </summary>
    public interface IFlowPayload
    {
        /// <summary>
        /// 把自身等分成 count 份。总量守恒——分完的总和与分摊前相等。
        /// 不是复制：不是 <c>count * 原值</c>，而是每份 <c>原值 / count</c>。
        /// </summary>
        /// <param name="count">要分成的份数，须 ≥ 1。</param>
        IReadOnlyList<IFlowPayload> Split(int count);

        /// <summary>与另一份载荷合并，返回合并产物。</summary>
        IFlowPayload Merge(IFlowPayload other);
    }
}
