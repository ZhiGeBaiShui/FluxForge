using System;

namespace FluxForge
{
    /// <summary>
    /// 装置状态播报。
    ///
    /// 一个装置最多挂一个反应类 + 一个路由类，两者都可以实现本接口。
    /// mod 层订阅后据此同步外部表示（tag / 显示 / 从载体上移除），
    /// 不需要每帧去轮询装置状态。
    ///
    /// 继承 <see cref="IFlowState"/>：播报者必须能暴露自己的状态，
    /// 订阅方不必再把 sender 向下强转。实现者必须是 <see cref="IFlowState.Keys"/>
    /// 所声明槽位的权威；<see cref="FlowStock.Write"/> 只遍历这些 key，
    /// 因此只关心部分属性的装置可以只声明自己拥有的槽位。
    /// </summary>
    public interface IFlowNotifier : IFlowState
    {
        /// <summary>装置存量发生变化（吸收了流源 / 完成了反应）。</summary>
        event Action<IFlowNotifier> Changed;

        /// <summary>装置已耗尽，应当从它所在的载体上移除。</summary>
        event Action<IFlowNotifier> Exhausted;
    }
}
