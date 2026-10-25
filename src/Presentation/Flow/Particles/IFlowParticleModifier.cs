using ProbablyBagMod.Presentation.Flow.Visual;

namespace ProbablyBagMod.Presentation.Flow.Particles
{
    /// <summary>
    /// 粒子 profile 的扩展点。实现必须**无状态、可重复调用**：
    /// 只读取 <paramref name="input"/>，只就地修改 <paramref name="profile"/>，
    /// 不缓存、不分配、不依赖调用次数或线程，否则同一份输入会得到不同 profile。
    ///
    /// 以后接毒物时的示例（**本轮不实现**）：
    /// <code>
    /// public sealed class PoisonParticleModifier : IFlowParticleModifier
    /// {
    ///     public void Apply(in FlowFrameInput input, ref FlowParticleProfile profile)
    ///     {
    ///         if (input.Composition.Poison &lt;= 0f) return;
    ///         profile.SpawnRate *= 0.5f;   // 毒物粒子更稀疏
    ///     }
    /// }
    /// FlowParticleRules.Register(new PoisonParticleModifier());
    /// </code>
    /// 注意：这个接口只能改 profile；「有概率生成暗绿色粒子」还需要颜色/概率，
    /// 见 <see cref="FlowParticleRules.ColorFor"/> 的扩展说明。
    /// </summary>
    public interface IFlowParticleModifier
    {
        /// <summary>在默认 profile 上就地调整。<paramref name="input"/> 是只读快照，禁止修改。</summary>
        void Apply(in FlowFrameInput input, ref FlowParticleProfile profile);
    }
}
