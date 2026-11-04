using System;

namespace FluxForge
{
    /// <summary>
    /// 装置状态播报。
    ///
    /// 一个装置最多挂一个反应类 + 一个路由类，两者都可以实现本接口。
    /// mod 层订阅后据此同步外部表示（tag / 显示 / 从载体上移除），
    /// 不需要每帧去轮询装置状态。
    /// </summary>
    public interface IFlowNotifier
    {
        /// <summary>装置存量发生变化（吸收了流源 / 完成了反应）。</summary>
        event Action<IFlowNotifier> Changed;

        /// <summary>装置已耗尽，应当从它所在的载体上移除。</summary>
        event Action<IFlowNotifier> Exhausted;
    }
}
