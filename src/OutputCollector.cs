using System;

namespace ProbablyBagMod
{
    /// <summary>
    /// 输出口：流源的收集点。
    ///
    /// 实现 <see cref="IFlowOutput"/> 标记后，框架检测到流源进入本装置
    /// 就直接收集并触发 <see cref="FlowSimulator.OnFlowReachExit"/>，
    /// **不会**调用 <see cref="OnFlowEnter"/>。
    ///
    /// 因此这里只是满足接口要求，返回空集合。
    /// </summary>
    public sealed class OutputCollector : IFlowOutput
    {
        public IReadOnlyList<FlowOutput> OnFlowEnter(FlowToken incoming, Direction entrySide)
        {
            return Array.Empty<FlowOutput>();
        }
    }
}
