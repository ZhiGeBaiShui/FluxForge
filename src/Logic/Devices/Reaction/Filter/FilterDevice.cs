namespace FluxForge
{
    /// <summary>
    /// 过滤装置：流源穿过时按 <see cref="FilterReaction"/> 去除化学污染与重金属。
    ///
    /// 无状态——滤材不消耗，装置只提供「穿过瞬间的净化」。
    /// 因此不实现 <see cref="IFlowState"/>（没有需要 tag 回灌的槽位），
    /// 也不实现 <see cref="IFlowNotifier"/>（没有状态变化要播报）；
    /// 只通过 <see cref="FlowAudio"/> / <see cref="FlowAnimation"/> 播报本次去除量。
    /// </summary>
    public sealed class FilterDevice : IFlowDevice
    {
        /// <summary>反应入口：流源撞上过滤装置时由运动层调用。</summary>
        /// <param name="entity">完整流源；过滤只读取速度、原地修改属性。</param>
        /// <returns>恒为 false——过滤不截停流源。</returns>
        public bool TryReact(FlowEntity entity)
        {
            double chemicalBefore = entity.Properties.Get(FlowProps.ChemicalContaminant);
            double heavyMetalBefore = entity.Properties.Get(FlowProps.HeavyMetal);

            FilterReaction.React(entity.Properties, entity.Motion.Speed);

            double chemicalRemoved =
                chemicalBefore - entity.Properties.Get(FlowProps.ChemicalContaminant);
            double heavyMetalRemoved =
                heavyMetalBefore - entity.Properties.Get(FlowProps.HeavyMetal);

            double removed = chemicalRemoved + heavyMetalRemoved;
            if (removed > 0d)
            {
                FlowAudio.Play(FlowSoundIds.FilterCleaned, this, removed);
                FlowAnimation.Play(FlowAnimationIds.FilterPulse, this, removed);
            }

            return false;
        }
    }
}
