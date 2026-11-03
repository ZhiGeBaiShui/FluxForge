using FluxForge.Presentation.Flow.Visual;

namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>
    /// 粒子 profile 的扩展点。实现必须**无状态、可重复调用**：
    /// 只读取 <paramref name="input"/>，只就地修改 <paramref name="profile"/>，
    /// 不缓存、不分配、不依赖调用次数或线程，否则同一份输入会得到不同 profile。
    ///
    /// 示例（当前快照只携带温度/含量/状态/本体色）：
    /// <code>
    /// public sealed class EmptyContentModifier : IFlowParticleModifier
    /// {
    ///     public void Apply(in FlowFrameInput input, ref FlowParticleProfile profile)
    ///     {
    ///         // 空档是 6×1 干痕，粒子减半，避免干痕看起来像在流动
    ///         if (input.Content != FlowContent.Empty) return;
    ///         profile.SpawnRate *= 0.5f;
    ///     }
    /// }
    /// FlowParticleRules.Register(new EmptyContentModifier());
    /// </code>
    /// 注意：<see cref="FlowFrameInput"/> 已不再携带六分量成分权重，modifier 无法按「毒物占比」
    /// 分支；要恢复这类规则需先把成分信息带进快照。本接口只能改 profile；
    /// 「有概率生成暗绿色粒子」还需要颜色/概率，见 <see cref="FlowParticleRules.ColorFor"/> 的扩展说明。
    /// </summary>
    public interface IFlowParticleModifier
    {
        /// <summary>在默认 profile 上就地调整。<paramref name="input"/> 是只读快照，禁止修改。</summary>
        void Apply(in FlowFrameInput input, ref FlowParticleProfile profile);
    }
}
