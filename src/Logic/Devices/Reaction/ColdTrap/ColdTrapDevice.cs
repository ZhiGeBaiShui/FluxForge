using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 冷阱装置：流源穿过时抽走其中含量最低的液体成分，并按需改变流源温度。
    ///
    /// 抽提量由装置配置决定：入口温度经温度变化后跨越相变阈值时乘效率倍率，
    /// 否则只取基础量。抽出的液体按体积与装置存量混合温度并存入
    /// <see cref="Extracted"/>；存量变化播报 <see cref="Changed"/>，
    /// 由 mod 层适配器同步到物品 tag。
    ///
    /// 返回 false = 不截停流源——冷阱只改属性、不改流向。
    /// 本类不认识 GameItem / 适配层 / 物品定义。
    /// </summary>
    public sealed class ColdTrapDevice : IFlowDevice, IFlowNotifier
    {
        private readonly double _temperatureDeltaCelsius;
        private readonly double _phaseThresholdCelsius;
        private readonly int _baseExtractionMicrolitres;
        private readonly double _thresholdEfficiency;
        private readonly int _capacityMicrolitres;

        private FlowProperties _extracted = new FlowProperties();

        /// <summary>装置存量：抽提得到的液体成分及其混合温度。</summary>
        public FlowProperties Extracted
        {
            get { return _extracted; }
        }

        /// <inheritdoc/>
        public event Action<IFlowNotifier> Changed;

        /// <summary>冷阱不会耗尽，本事件为接口占位，永不触发。</summary>
        public event Action<IFlowNotifier> Exhausted
        {
            add { }
            remove { }
        }

        /// <summary>按配置初始化冷阱。参数不做校验，取值由物品定义与适配层保证。</summary>
        /// <param name="temperatureDeltaCelsius">每次抽提时对流源施加的温度变化（摄氏度）；正升温，负降温。</param>
        /// <param name="phaseThresholdCelsius">相变温度阈值（摄氏度）。</param>
        /// <param name="baseExtractionMicrolitres">未跨越阈值时的基础抽提量（微升）。</param>
        /// <param name="thresholdEfficiency">跨越阈值时基础抽提量的倍率。</param>
        /// <param name="capacityMicrolitres">装置存量上限（微升）。</param>
        public ColdTrapDevice(
            double temperatureDeltaCelsius,
            double phaseThresholdCelsius,
            int baseExtractionMicrolitres,
            double thresholdEfficiency,
            int capacityMicrolitres)
        {
            _temperatureDeltaCelsius = temperatureDeltaCelsius;
            _phaseThresholdCelsius = phaseThresholdCelsius;
            _baseExtractionMicrolitres = baseExtractionMicrolitres;
            _thresholdEfficiency = thresholdEfficiency;
            _capacityMicrolitres = capacityMicrolitres;
        }

        /// <summary>
        /// 整体替换装置存量（读档 / 适配层回灌），不播报 <see cref="Changed"/>。
        /// </summary>
        /// <param name="extracted">新的存量；直接持有该对象，不做拷贝。</param>
        public void ReplaceExtracted(FlowProperties extracted)
        {
            _extracted = extracted;
        }

        /// <summary>
        /// 反应入口：流源撞上冷阱时由运动层调用。
        ///
        /// 从流源里挑出含量最低的正液体成分（同量时按注册表顺序取靠前者），
        /// 受存量剩余容量与可用量限制后抽入 <see cref="Extracted"/>；
        /// 入口温度经温度变化后跨越相变阈值时，再把流源温度置为出口温度。
        /// </summary>
        /// <param name="entity">完整流源；冷阱只读取并原地修改其属性。</param>
        /// <returns>恒为 false——冷阱不截停流源。</returns>
        public bool TryReact(FlowEntity entity)
        {
            FlowProperties properties = entity.Properties;

            // 注册表顺序即优先级：只有严格更低的量才会替换当前目标，同量保留先出现者。
            FlowPropertyDef target = null;
            double lowest = 0d;
            IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;
            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                if (def.Kind != FlowPropertyKind.Liquid)
                {
                    continue;
                }

                double value = properties.Get(def.Key);
                if (value > 0d && (target == null || value < lowest))
                {
                    target = def;
                    lowest = value;
                }
            }

            if (target == null)
            {
                return false;
            }

            double inlet = properties.Get(FlowProps.Temperature);
            double outlet = inlet + _temperatureDeltaCelsius;
            bool crossed = inlet > _phaseThresholdCelsius && outlet < _phaseThresholdCelsius;

            double rawAmount = _baseExtractionMicrolitres * (crossed ? _thresholdEfficiency : 1d);
            int amount = (int)rawAmount;

            int available = (int)properties.Get(target.Key);
            int free = _capacityMicrolitres - (int)_extracted.SubstanceTotal;
            int removed = Math.Min(amount, Math.Min(available, free));
            if (removed <= 0)
            {
                return false;
            }

            properties.Set(target.Key, properties.Get(target.Key) - removed);

            // 按体积加权混合存量温度；存量还空时直接采用入口温度。
            double oldVolume = _extracted.SubstanceTotal;
            double oldTemp = _extracted.Get(FlowProps.Temperature);
            _extracted.Set(target.Key, _extracted.Get(target.Key) + removed);

            double newVolume = oldVolume + removed;
            double newTemp = oldVolume <= 0d
                ? inlet
                : (oldTemp * oldVolume + inlet * removed) / newVolume;
            _extracted.Set(FlowProps.Temperature, newTemp);

            if (crossed)
            {
                properties.Set(FlowProps.Temperature, outlet);
            }

            FlowAudio.Play(
                crossed ? FlowSoundIds.ExtractedCross : FlowSoundIds.Extracted,
                this,
                removed);
            Changed?.Invoke(this);
            return false;
        }
    }
}
