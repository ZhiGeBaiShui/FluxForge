namespace ProbablyBagMod
{
    /// <summary>
    /// 网格查询：运动层知道「某格上有什么」的唯一通道。由外部（适配层）实现，框架不认识游戏网格。
    ///
    /// 路由 / 反应两个查询分开（而不是返回一个「装置」对象）：
    /// 一个格子上的东西可能只实现其中之一、两者都实现、或都不实现（空格），
    /// 分开查询让运动层直接判空后调用，不用做 is 类型判断。
    /// </summary>
    public interface IFlowGrid
    {
        /// <summary>查询格子上的路由装置（分流 / 转向）。没有返回 null。</summary>
        IFlowRouter GetRouterAt(int cellX, int cellY);

        /// <summary>查询格子上的反应装置（改属性）。没有返回 null。</summary>
        IFlowDevice GetReactorAt(int cellX, int cellY);
    }
}
