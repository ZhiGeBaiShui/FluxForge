namespace ProbablyBagMod
{
    /// <summary>
    /// 网格查询接口：由外部（游戏适配层）实现。
    /// 框架通过它知道「某个格子上是什么装置」。
    /// </summary>
    public interface IFlowGrid
    {
        /// <summary>取指定格子上的装置；空格或越界返回 null。</summary>
        IFlowDevice GetDeviceAt(GridPos pos);
    }
}
