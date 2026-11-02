using FluxForge.Presentation.Flow.Visual;
namespace FluxForge.Presentation.Flow.Particles
{
    /// <summary>
    /// 默认注册的温度 modifier：温度只改**扩散速度**与**寿命**，不改颜色、不改数量。
    ///
    ///   · cold：速度 ×0.6、寿命 ×1.2；
    ///   · normal：不变；
    ///   · hot：速度 ×1.6、寿命 ×0.8。
    ///
    /// 无状态、无分配；同一个实例可以安全地被全局注册表复用。倍率常量在
    /// <see cref="FlowParticleTuning"/>，以后调参不要改这里。
    /// </summary>
    public sealed class TemperatureSpeedModifier : IFlowParticleModifier
    {
        /// <summary>全局唯一实例。<see cref="FlowParticleRules"/> 的静态构造只注册它。</summary>
        public static readonly TemperatureSpeedModifier Instance = new TemperatureSpeedModifier();

        private TemperatureSpeedModifier()
        {
        }

        /// <summary>按温度档原地缩放速度/寿命区间；未知温度按 normal 处理。</summary>
        public void Apply(in FlowFrameInput input, ref FlowParticleProfile profile)
        {
            switch (input.Temp)
            {
                case FlowTemp.Cold:
                    profile.SpeedMin *= FlowParticleTuning.ColdSpeedScale;
                    profile.SpeedMax *= FlowParticleTuning.ColdSpeedScale;
                    profile.LifeMin *= FlowParticleTuning.ColdLifeScale;
                    profile.LifeMax *= FlowParticleTuning.ColdLifeScale;
                    break;

                case FlowTemp.Hot:
                    profile.SpeedMin *= FlowParticleTuning.HotSpeedScale;
                    profile.SpeedMax *= FlowParticleTuning.HotSpeedScale;
                    profile.LifeMin *= FlowParticleTuning.HotLifeScale;
                    profile.LifeMax *= FlowParticleTuning.HotLifeScale;
                    break;

                default:
                    // normal（以及越界枚举值）保持默认 profile。
                    break;
            }
        }
    }
}
