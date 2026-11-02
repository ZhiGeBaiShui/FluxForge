using FluxForge.Presentation.Flow.Visual;
namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>
    /// 常驻环境粒子的可调参数。**纯值类型、零 Unity 依赖**，可以在测试项目里直接构造。
    ///
    /// 单位约定（与 <c>docs/flow-visual/final/SPEC.md</c> 一致）：
    ///   · 距离 / 位置 / 尺寸都是「格」，1.0 = 一格 = 16 个设计像素；
    ///   · 速度是「格/秒」；寿命是「秒」；
    ///   · <see cref="AlphaStart"/> 是 0..1 的起始不透明度，实际绘制时再乘寿命比例。
    ///
    /// 本结构由 <see cref="FlowParticleRules.Build"/> 从
    /// <see cref="FlowParticleTuning"/> 的默认值生成，再依次交给已注册的
    /// <see cref="IFlowParticleModifier"/> 就地调整；不要在调用方硬编码这些数字。
    /// </summary>
    public struct FlowParticleProfile
    {
        /// <summary>初速下限（格/秒）。</summary>
        public float SpeedMin;

        /// <summary>初速上限（格/秒）；必须 ≥ <see cref="SpeedMin"/>。</summary>
        public float SpeedMax;

        /// <summary>寿命下限（秒）。</summary>
        public float LifeMin;

        /// <summary>寿命上限（秒）；必须 ≥ <see cref="LifeMin"/>。</summary>
        public float LifeMax;

        /// <summary>起始不透明度（0..1）。粒子越老越淡，绘制层按 <c>Life / MaxLife</c> 再乘这个值。</summary>
        public float AlphaStart;

        /// <summary>单个粒子的边长（格）。默认 1/16 格 = 1 个设计像素。</summary>
        public float SizeCells;

        /// <summary>
        /// 向运动反方向的偏置（0..1）。0 = 纯径向散开；1 = 反方向分量与径向等强。
        /// 用来让粒子看起来像被流源「拖」在身后，而不是均匀炸开。
        /// </summary>
        public float BackwardBias;

        /// <summary>每秒每个流源生成的粒子数。</summary>
        public float SpawnRate;

        /// <summary>
        /// 扩散半径（格）。粒子离出生点超过该距离后，寿命按超出量加速扣除
        /// （见 <see cref="FlowParticleTuning.SpreadFadeRate"/>），表现为越散越淡、更快消失。
        /// </summary>
        public float SpreadRadius;
    }
}
