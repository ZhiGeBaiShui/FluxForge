namespace ProbablyBagMod
{
    /// <summary>
    /// 输出口标记接口：实现它的装置视为「收集点」。
    /// 流源进入后不再流向他处，由框架收集并触发 <see cref="FlowSimulator.OnFlowReachExit"/>。
    /// </summary>
    public interface IFlowOutput : IFlowDevice
    {
    }
}
