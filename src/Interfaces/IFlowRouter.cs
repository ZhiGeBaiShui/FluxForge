namespace ProbablyBagMod
{
    /// <summary>
    /// 路由接口：运动侧装置对外提供的「流源再分配」函数。
    ///
    /// 与反应框架（<see cref="IFlowDevice.TryReact"/>——改流源属性）明确分工：
    ///   · 反应管「流源变成什么」（属性变化）
    ///   · 本接口管「流源去哪、分几路」（数量与方向的变化）
    ///
    /// 运动层发现流源撞上本装置时调用本方法。装置自行决定怎么再分配：
    /// 通过 <see cref="FlowSpawner.Instance"/> 把新流源一个个注册出去
    /// （注册 0 个 = 吸收，1 个 = 直通/转向，N 个 = 分流）。
    /// 注册完就不归它管——延迟期间流源在容器里排队，到点生成后归运动层管理。
    ///
    /// 分流、汇聚、延迟重放都是「路由」的不同形态：
    ///   · 分流：1 个流入 → 注册 N 个新流源
    ///   · 汇聚：N 个流入（每次调用收 1 个，装置内部 buffer 累积）→ 凑齐后注册 1 个
    ///   · 延迟重放：照常路由，只是注册时给非 0 延迟
    /// </summary>
    public interface IFlowRouter
    {
        /// <summary>
        /// 对流入的流源做路由（再分配）。
        /// </summary>
        /// <param name="incoming">流入的流源（实参）。装置自行读取其位置/运动数据/属性。</param>
        void Route(FlowEntity incoming);
    }
}
