using System;

namespace FluxForge
{
    /// <summary>
    /// 温度控制装置：流源穿过时把装置自身携带的温度值加到流源上，
    /// 并把结果限制在 <see cref="MinTemperature"/> 与 <see cref="MaxTemperature"/> 之间。
    ///
    /// 升温装置和降温装置装配同一个类，区别只在 items.json 里给的
    /// <c>FLUXFORGE_TEMPERATURE_DELTA</c> 值：
    /// <code>
    /// "tags": [{ "valueName": "FLUXFORGE_TEMPERATURE_DELTA", "valueDouble": 10.0 }]   // 升温 10 度
    /// "tags": [{ "valueName": "FLUXFORGE_TEMPERATURE_DELTA", "valueDouble": -10.0 }]  // 降温 10 度
    /// </code>
    ///
    /// 只改属性、不改流向，所以不实现 <see cref="IFlowRouter"/>（没有 Facing）。
    /// 温度配置来自 items.json，运行期不变，所以也没有状态播报事件。
    /// </summary>
    public sealed class TemperatureDevice : IFlowDevice
    {
        /// <summary>
        /// 本装置对每次穿过的流源施加的温度增量。
        /// 正 = 升温这么多度，负 = 降温这么多度，0 = 什么都不做。
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>流源经过本装置后允许达到的最高温度。</summary>
        public double MaxTemperature { get; }

        /// <summary>流源经过本装置后允许达到的最低温度。</summary>
        public double MinTemperature { get; }

        /// <summary>按温度增量、温度上限、温度下限初始化。</summary>
        public TemperatureDevice(
            double temperature,
            double maxTemperature,
            double minTemperature)
        {
            Temperature = temperature;
            MaxTemperature = maxTemperature;
            MinTemperature = minTemperature;
        }

        /// <summary>
        /// 反应入口：把 <see cref="Temperature"/> 加到流源温度上，
        /// 再把结果限制到上下限之间。
        ///
        /// 返回 false = 不截停。温度变化是瞬时的，流源不需要停在装置格；
        /// 截停与否都不影响触发次数——运动层只在跨越格子边界时 probe 一次。
        /// </summary>
        /// <param name="entity">完整流源；本装置只读温度、写温度。</param>
        public bool TryReact(FlowEntity entity)
        {
            double current = entity.Properties.Get(FlowProps.Temperature);
            double next = current + Temperature;

            if (next > MaxTemperature)
            {
                next = MaxTemperature;
            }
            else if (next < MinTemperature)
            {
                next = MinTemperature;
            }

            entity.Properties.Set(FlowProps.Temperature, next);

            double applied = next - current;
            if (applied != 0d)
            {
                entity.HoldSeconds = FlowReactionDelay.NextSeconds();
                FlowAudio.Play(
                    applied > 0d ? FlowSoundIds.Heated : FlowSoundIds.Cooled,
                    this,
                    Math.Abs(applied));
                FlowAnimation.Play(FlowAnimationIds.TemperaturePulse, this, Math.Abs(applied));
            }

            return false;
        }
    }
}
