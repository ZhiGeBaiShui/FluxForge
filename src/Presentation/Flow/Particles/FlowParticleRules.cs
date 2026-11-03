using System;
using System.Threading;
using FluxForge.Presentation.Flow.Visual;

namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>
    /// 常驻粒子的纯函数规则层：默认 profile → 已注册 modifier → 最终 profile，以及颜色。
    ///
    /// 线程模型：
    ///   · <see cref="Register"/> 只在启动期调用，内部加锁并整体替换不可变数组；
    ///   · <see cref="Build"/> 只读快照，不加锁、不分配，可被任意线程并发调用；
    ///   · modifier 自身必须无状态（见 <see cref="IFlowParticleModifier"/>），
    ///     因此同一份输入无论何时、在哪个线程构建，结果都相同。
    /// 顺序：modifier 按注册顺序依次执行；实现不应依赖其它 modifier 的结果。
    /// </summary>
    public static class FlowParticleRules
    {
        /// <summary>白色，用于默认颜色向白混合 10%。</summary>
        private static readonly FlowColor White = new FlowColor(0xFF, 0xFF, 0xFF);

        /// <summary>注册表写锁。只有 <see cref="Register"/> 会进入，Build 走无锁快照。</summary>
        private static readonly object RegistryGate = new object();

        /// <summary>
        /// 不可变数组快照：Register 时整体替换，Build 通过 <see cref="Volatile.Read{T}(ref T)"/>
        /// 读取一个完整版本。数组内容在发布后不再修改，因此读侧无需加锁。
        /// </summary>
        private static IFlowParticleModifier[] _modifiers = Array.Empty<IFlowParticleModifier>();

        /// <summary>
        /// 静态构造：默认注册 <see cref="TemperatureSpeedModifier"/>。
        /// 必须在任何 <see cref="Build"/> / <see cref="Register"/> 之前完成，
        /// 保证调用方无需手动初始化。
        /// </summary>
        static FlowParticleRules()
        {
            Register(TemperatureSpeedModifier.Instance);
        }

        /// <summary>
        /// 从 <see cref="FlowParticleTuning"/> 取默认值，再依次跑已注册的 modifier。
        ///
        /// 纯函数：不修改 <paramref name="input"/>，不读写实例字段，稳态 0 分配。
        /// </summary>
        public static FlowParticleProfile Build(in FlowFrameInput input)
        {
            // 字段按「速度 → 寿命 → 外观 → 数量/扩散」分组，便于对照 tuning。
            FlowParticleProfile profile = new FlowParticleProfile
            {
                SpeedMin = FlowParticleTuning.SpeedMin,
                SpeedMax = FlowParticleTuning.SpeedMax,
                LifeMin = FlowParticleTuning.LifeMin,
                LifeMax = FlowParticleTuning.LifeMax,
                AlphaStart = FlowParticleTuning.AlphaStart,
                SizeCells = FlowParticleTuning.SizeCells,
                BackwardBias = FlowParticleTuning.BackwardBias,
                SpawnRate = FlowParticleTuning.SpawnRate,
                SpreadRadius = FlowParticleTuning.SpreadRadius,
            };

            IFlowParticleModifier[] modifiers = Volatile.Read(ref _modifiers);
            for (int i = 0; i < modifiers.Length; i++)
            {
                modifiers[i].Apply(in input, ref profile);
            }

            return profile;
        }

        /// <summary>
        /// 注册一个 profile modifier。扩展点：任何实现 <see cref="IFlowParticleModifier"/>
        /// 的无状态类都可以在启动期追加，Build 会自动带上它。
        ///
        /// 只允许在启动期/测试初始化时调用；运行时反复注册会让数组不断增长。
        /// <paramref name="modifier"/> 为 null 时抛 <see cref="ArgumentNullException"/>。
        /// </summary>
        public static void Register(IFlowParticleModifier modifier)
        {
            if (modifier == null)
            {
                throw new ArgumentNullException(nameof(modifier));
            }

            lock (RegistryGate)
            {
                IFlowParticleModifier[] current = _modifiers;
                IFlowParticleModifier[] next = new IFlowParticleModifier[current.Length + 1];
                Array.Copy(current, next, current.Length);
                next[current.Length] = modifier;

                // 发布完整的新数组；读侧永远看不到「写了一半」的注册表。
                Volatile.Write(ref _modifiers, next);
            }
        }

        /// <summary>
        /// 粒子颜色。默认 = <c>mix(bodyColor, white, 0.10)</c>：
        /// 与本体同色但略提亮，避免和贴图本体糊成一块。
        ///
        /// **扩展方式（当前未实现）**：以后要按成分改色（例如毒物生成暗绿色粒子）时，
        /// 需要在 <c>ReadSnapshot</c> 侧按液体存量先算出 <see cref="FlowFrameInput.BodyColor"/>，
        /// 本方法只负责「本体色 → 粒子色」的纯映射（<see cref="FlowFrameInput"/> 不再携带六分量权重）。
        /// 注意：本方法必须是**纯函数**，不要在这里用随机数决定「有概率」；
        /// 概率应由调用方在每次 Spawn 前掷骰，再把选好的颜色传给
        /// <see cref="FlowAmbientField.Spawn"/>。这样同一份输入永远得到同一个颜色，测试可重复。
        /// </summary>
        public static FlowColor ColorFor(in FlowFrameInput input)
        {
            FlowColor body = input.BodyColor;
            return FlowColor.Mix(body, White, 0.10d);
        }
    }
}
