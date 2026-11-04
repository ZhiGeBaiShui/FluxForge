namespace FluxForge
{
    /// <summary>
    /// 路由接口：运动侧装置对外提供的「流源再分配」函数。
    ///
    /// 与反应框架（<see cref="IFlowDevice.TryReact"/>——改流源属性）明确分工：
    ///   · 反应管「流源变成什么」（属性变化）
    ///   · 本接口管「流源去哪、分几路」（数量与方向的变化）
    ///
    /// 运动层发现流源撞上本装置时调用本方法。装置自行决定怎么再分配：
    /// 通过传入的 <see cref="FlowSpawner"/> 接受新流源或终结旧流源
    /// （终结 = 吸收，接受 1 个 = 直通/转向，接受 N 个 = 分流）。
    ///
    /// 生命周期由调用方传进来而不是装置自己持有：
    /// 一块网格一份名单，装置不该知道自己在哪块网格上。
    ///
    /// 装置格由运动层传入而不是让装置自己从 <c>incoming.Position</c> 推算：
    /// 一帧跨多格时（卡顿、提速）当前位置离装置可能隔了好几格，推算必错。
    ///
    /// 分流、汇聚、延迟重放都是「路由」的不同形态：
    ///   · 分流：1 个流入 → 接受 N 个新流源
    ///   · 汇聚：N 个流入（每次调用收 1 个，装置内部 buffer 累积）→ 凑齐后接受 1 个
    ///   · 吸收：1 个流入 → 终结它
    /// </summary>
    public interface IFlowRouter
    {
        /// <summary>
        /// 朝向（0=上 1=右 2=下 3=左）。
        ///
        /// 路由装置才有方向：入口/出口按「相对朝向」定义，物品旋转时由
        /// mod 层按 <c>modifiedShape.orientation</c> 同步，装置内部据此换算世界方向。
        /// 纯反应装置（只改属性、不改流向）没有这个属性。
        /// </summary>
        int Facing { get; set; }

        /// <summary>
        /// 对流入的流源做路由（再分配）。
        /// </summary>
        /// <param name="incoming">流入的流源（实参）。装置自行读取其位置/运动数据/属性。</param>
        /// <param name="flows">提供这份名单的流源生命周期（接受新流源 / 终结旧流源）。</param>
        /// <param name="cellX">装置所在格（流源即将进入的那一格）。</param>
        /// <param name="cellY">装置所在格。</param>
        /// <returns>是否截停流源（true = 由本装置接管，不再继续前进）。</returns>
        bool Route(FlowEntity incoming, FlowSpawner flows, int cellX, int cellY);
    }
}
