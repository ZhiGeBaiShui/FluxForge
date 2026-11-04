namespace FluxForge
{
    /// <summary>
    /// 过滤装置：流源穿过时按 <see cref="FilterReaction"/> 去除化学污染与重金属。
    ///
    /// 无状态、无事件——滤材不消耗，装置只提供「穿过瞬间的净化」。
    /// 因此不实现 <see cref="IFlowState"/>（没有需要 tag 回灌的槽位），
    /// 也不实现 <see cref="IFlowNotifier"/>（没有状态变化要播报）。
    ///
    /// 与运动层的关系：
    ///   流源跨越到本装置格时，<see cref="FlowManager.TryProbeCell"/> 调用
    ///   <see cref="TryReact"/>。返回 <c>false</c> = 不截停，流源直接穿过继续移动。
    ///
    /// 为什么不返回 true：截停会把流源拉到装置格中心再重新起步，过滤不需要这个停顿。
    /// 而且截停与否都不影响净化次数——运动层只在跨越边界时 probe 一次，
    /// 流源停在格中心后下一帧是从该格往外走，不会再 probe 同一格。
    /// </summary>
    public sealed class FilterDevice : IFlowDevice
    {
        /// <summary>反应入口：流源撞上过滤装置时由运动层调用。</summary>
        /// <param name="entity">完整流源；过滤只读取速度、原地修改属性。</param>
        /// <returns>恒为 false——过滤不截停流源。</returns>
        public bool TryReact(FlowEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            FilterReaction.React(entity.Properties, entity.Motion.Speed);
            return false;
        }
    }
}
