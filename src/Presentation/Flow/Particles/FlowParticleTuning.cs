namespace ProbablyBagMod.Presentation.Flow.Particles
{
    /// <summary>
    /// 常驻粒子的默认参数与倍率常量。**纯常量、零 Unity 依赖**。
    ///
    /// 当前数值是占位值，之后会按设计探针微调；调参只改这里，
    /// 不要改 <see cref="FlowParticleRules.Build"/> 或各个 modifier 里的字面量。
    /// </summary>
    public static class FlowParticleTuning
    {
        /// <summary>默认每秒每流源生成数。</summary>
        public const float SpawnRate = 8f;

        /// <summary>默认初速下限（格/秒）。</summary>
        public const float SpeedMin = 0.6f;

        /// <summary>默认初速上限（格/秒）。</summary>
        public const float SpeedMax = 1.4f;

        /// <summary>默认寿命下限（秒）。</summary>
        public const float LifeMin = 0.40f;

        /// <summary>默认寿命上限（秒）。</summary>
        public const float LifeMax = 0.65f;

        /// <summary>默认起始不透明度（0..1）。</summary>
        public const float AlphaStart = 0.45f;

        /// <summary>默认粒子边长（格）：1/16 格 = 1 个设计像素。</summary>
        public const float SizeCells = 1f / 16f;

        /// <summary>默认反方向偏置（0..1）。</summary>
        public const float BackwardBias = 0.30f;

        /// <summary>默认扩散半径（格）。</summary>
        public const float SpreadRadius = 0.5f;

        /// <summary>
        /// 冷档速度倍率：0.6×。数值放在这里，便于以后与设计探针一起调。
        /// </summary>
        public const float ColdSpeedScale = 0.6f;

        /// <summary>热档速度倍率：1.6×。</summary>
        public const float HotSpeedScale = 1.6f;

        /// <summary>冷档寿命倍率：1.2×（冷得慢，粒子活得久一点）。</summary>
        public const float ColdLifeScale = 1.2f;

        /// <summary>热档寿命倍率：0.8×（热得快，粒子更短促）。</summary>
        public const float HotLifeScale = 0.8f;

        /// <summary>
        /// 扩散超半径后的额外寿命扣除率：每超出 1 格、每秒额外扣除多少「秒寿命」。
        /// 取 4 时，超出 0.5 格的粒子寿命按 3× 速度蒸发；具体值留给设计探针调。
        /// </summary>
        public const float SpreadFadeRate = 4f;
    }
}
