using System;

namespace FluxForge
{
    /// <summary>
    /// 温度控制装置：流源穿过时把装置自身携带的温度值加到流源上。
    ///
    /// 升温装置和降温装置装配同一个类，区别只在 items.json 里给的
    /// <c>CONTAINER_TEMP_TAG</c> 值：
    /// <code>
    /// "tags": [{ "valueName": "CONTAINER_TEMP_TAG", "valueDouble": 10.0 }]   // 升温 10 度
    /// "tags": [{ "valueName": "CONTAINER_TEMP_TAG", "valueDouble": -10.0 }]  // 降温 10 度
    /// </code>
    ///
    /// 只改属性、不改流向，所以不实现 <see cref="IFlowRouter"/>（没有 Facing）。
    /// 温度是装置定义的一部分、运行期不变，所以也没有状态播报事件；
    /// 初始温度由 mod 层适配器在创建时给出（构造函数或公开的 <see cref="Temperature"/>）。
    /// </summary>
    public sealed class TemperatureDevice : IFlowDevice
    {
        /// <summary>
        /// 本装置对每次穿过的流源施加的温度增量。
        /// 正 = 升温这么多度，负 = 降温这么多度，0 = 什么都不做。
        /// </summary>
        public double Temperature { get; set; }

        /// <summary>按给定温度增量初始化。</summary>
        public TemperatureDevice(double temperature)
        {
            Temperature = temperature;
        }

        /// <summary>
        /// 反应入口：把 <see cref="Temperature"/> 加到流源温度上。
        ///
        /// 返回 false = 不截停。温度变化是瞬时的，流源不需要停在装置格；
        /// 截停与否都不影响触发次数——运动层只在跨越格子边界时 probe 一次。
        /// </summary>
        /// <param name="entity">完整流源；本装置只读温度、写温度。</param>
        public bool TryReact(FlowEntity entity)
        {
            entity.Properties.Set(
                FlowProps.Temperature,
                entity.Properties.Get(FlowProps.Temperature) + Temperature);

            if (Temperature != 0d)
            {
                double amount = Math.Abs(Temperature);
                FlowAudio.Play(
                    Temperature > 0d ? FlowSoundIds.Heated : FlowSoundIds.Cooled,
                    this,
                    amount);
                FlowAnimation.Play(FlowAnimationIds.TemperaturePulse, this, amount);
            }

            return false;
        }
    }
}
