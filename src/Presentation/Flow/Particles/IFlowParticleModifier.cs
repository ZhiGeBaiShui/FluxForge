using ProbablyBagMod.Presentation.Flow.Visual;
namespace ProbablyBagMod.Presentation.Flow.Particles
{
    /// <summary>
    /// 构建粒子 profile 时可见的只读输入快照。**纯值类型、零 Unity 依赖**。
    ///
    /// 这是本轮留给「按属性加 modifier」的唯一扩展入口：
    ///   · <see cref="Composition"/> / <see cref="Temp"/> / <see cref="Content"/> / <see cref="State"/>
    ///     是流源当前的视觉属性；
    ///   · <see cref="Speed"/> 是流源当前运动速度（格/秒），暂时没有默认 modifier 使用它，
    ///     留给以后做「越快拖尾越长」之类的规则。
    /// </summary>
    public readonly struct FlowParticleInput
    {
        /// <summary>六分量成分（相对权重，不要求归一化）。</summary>
        public readonly FlowComposition Composition;

        /// <summary>温度档。本轮只由它驱动扩散速度/寿命。</summary>
        public readonly FlowTemp Temp;

        /// <summary>含量档。决定出生包围盒，本轮不参与默认 modifier。</summary>
        public readonly FlowContent Content;

        /// <summary>流源状态（flow/blocked/split/merge/collect）。留给以后按状态改粒子表现。</summary>
        public readonly FlowState State;

        /// <summary>流源当前运动速度（格/秒）。留给以后的速度相关 modifier。</summary>
        public readonly float Speed;

        /// <summary>构造一份输入快照。所有字段只读，构造后不可变。</summary>
        public FlowParticleInput(
            FlowComposition composition,
            FlowTemp temp,
            FlowContent content,
            FlowState state,
            float speed)
        {
            Composition = composition;
            Temp = temp;
            Content = content;
            State = state;
            Speed = speed;
        }
    }

    /// <summary>
    /// 粒子 profile 的扩展点。实现必须**无状态、可重复调用**：
    /// 只读取 <paramref name="input"/>，只就地修改 <paramref name="profile"/>，
    /// 不缓存、不分配、不依赖调用次数或线程，否则同一份输入会得到不同 profile。
    ///
    /// 以后接毒物时的示例（**本轮不实现**）：
    /// <code>
    /// public sealed class PoisonParticleModifier : IFlowParticleModifier
    /// {
    ///     public void Apply(in FlowParticleInput input, ref FlowParticleProfile profile)
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
        void Apply(in FlowParticleInput input, ref FlowParticleProfile profile);
    }
}
