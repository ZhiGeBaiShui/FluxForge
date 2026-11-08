using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 离心机：按进入速度选择可分离粒径，把可分离成分按固定系数 k 抽入自身存量。
    ///
    /// 一次通过：
    ///   x = 粒径 >= threshold 的可分离总量
    ///   y = 其余液体总量
    ///   x1 = max(0, x - k*y)
    ///
    /// x1 受装置剩余容量限制，按比例从各可分离成分中抽取，存入 <see cref="Extracted"/>。
    /// 未抽走的可分离成分和全部不可分离成分留在原流源中继续前进。
    ///
    /// 返回 false = 不截停流源——离心机只改属性、不做双出口分流。
    /// </summary>
    public sealed class CentrifugeDevice : IFlowDevice, IFlowNotifier
    {
        private readonly double _k;
        private readonly int _capacityMicrolitres;

        private FlowProperties _extracted = new FlowProperties();

        /// <summary>逻辑粒径；温度等标量不参与。</summary>
        private static readonly Dictionary<string, double> ParticleSizes
            = BuildParticleSizes();

        /// <summary>装置存量：离心出来的可分离成分及其混合温度。</summary>
        public FlowProperties Extracted
        {
            get { return _extracted; }
        }

        /// <summary>固定分离系数；越大越偏向保留轻相。</summary>
        public double K
        {
            get { return _k; }
        }

        /// <summary>存量上限（微升）。</summary>
        public int CapacityMicrolitres
        {
            get { return _capacityMicrolitres; }
        }

        /// <inheritdoc/>
        public event Action<IFlowNotifier> Changed;

        /// <summary>离心机不会耗尽，本事件为接口占位，永不触发。</summary>
        public event Action<IFlowNotifier> Exhausted
        {
            add { }
            remove { }
        }

        public CentrifugeDevice(double k, int capacityMicrolitres)
        {
            if (k < 1d || k > 10d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(k), "离心机系数 k 必须在 1～10 之间");
            }

            _k = k;
            _capacityMicrolitres = capacityMicrolitres;
        }

        /// <summary>整体替换存量（读档 / 适配层回灌），不播报 Changed。</summary>
        public void ReplaceExtracted(FlowProperties extracted)
        {
            _extracted = extracted;
        }

        /// <inheritdoc/>
        public bool TryReact(FlowEntity entity)
        {
            double speed = entity.Motion == null ? 0d : entity.Motion.Speed;
            double threshold = ThresholdForSpeed(speed);

            double separable = 0d;
            double nonSeparable = 0d;
            IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;

            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                if (def.Kind != FlowPropertyKind.Liquid)
                {
                    continue;
                }

                double amount = entity.Properties.Get(def.Key);
                if (amount <= 0d)
                {
                    continue;
                }

                if (IsSeparable(def.Id, threshold))
                {
                    separable += amount;
                }
                else
                {
                    nonSeparable += amount;
                }
            }

            double removable = separable - _k * nonSeparable;
            if (removable <= 0d || separable <= 0d)
            {
                return false;
            }

            int free = _capacityMicrolitres - (int)_extracted.SubstanceTotal;
            if (free <= 0)
            {
                return false;
            }

            int removed = (int)Math.Min(removable, free);
            if (removed <= 0)
            {
                return false;
            }

            double extractionRatio = removed / separable;
            double inletTemperature = entity.Properties.Get(FlowProps.Temperature);

            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                if (def.Kind != FlowPropertyKind.Liquid)
                {
                    continue;
                }

                double amount = entity.Properties.Get(def.Key);
                if (amount <= 0d || !IsSeparable(def.Id, threshold))
                {
                    continue;
                }

                double taken = amount * extractionRatio;
                entity.Properties.Set(def.Key, amount - taken);
                _extracted.Set(
                    def.Key,
                    _extracted.Get(def.Key) + taken);
            }

            double oldVolume = _extracted.SubstanceTotal - removed;
            double oldTemperature = _extracted.Get(FlowProps.Temperature);
            double newTemperature = oldVolume <= 0d
                ? inletTemperature
                : (oldTemperature * oldVolume + inletTemperature * removed)
                    / _extracted.SubstanceTotal;
            _extracted.Set(FlowProps.Temperature, newTemperature);

            FlowAudio.Play(FlowSoundIds.CentrifugeSpin, this, removed);
            Changed?.Invoke(this);
            return false;
        }

        /// <summary>速度越高，可分离粒径越小；25 以下不处理。</summary>
        public static double ThresholdForSpeed(double speed)
        {
            if (!(speed > 0d))
            {
                return 200d;
            }

            double threshold = 200d / speed;
            if (threshold < 25d)
            {
                return 25d;
            }

            if (threshold > 200d)
            {
                return 200d;
            }

            return threshold;
        }

        private static bool IsSeparable(string id, double threshold)
        {
            double size;
            return ParticleSizes.TryGetValue(id, out size)
                && size >= threshold;
        }

        private static Dictionary<string, double> BuildParticleSizes()
        {
            return new Dictionary<string, double>(StringComparer.Ordinal)
            {
                { "water", FlowParticleSizeConstants.Water },
                { "heavy_metal", FlowParticleSizeConstants.HeavyMetal },
                { "organic_waste", FlowParticleSizeConstants.OrganicWaste },
                { "microplastic", FlowParticleSizeConstants.Microplastic },
                { "microbe", FlowParticleSizeConstants.Microbe },
                { "chemical_contaminant", FlowParticleSizeConstants.ChemicalContaminant },
                { "physical_contaminant", FlowParticleSizeConstants.PhysicalContaminant },
                { "mineral", FlowParticleSizeConstants.Mineral },
                { "dream_cap_mush", FlowParticleSizeConstants.DreamCapMush },
                { "dream_cap_extract", FlowParticleSizeConstants.DreamCapExtract },
                { "plant_matter", FlowParticleSizeConstants.PlantMatter },
                { "alcohol", FlowParticleSizeConstants.Alcohol },
                { "caffeine", FlowParticleSizeConstants.Caffeine },
                { "protein", FlowParticleSizeConstants.Protein },
                { "fat", FlowParticleSizeConstants.Fat },
                { "starch", FlowParticleSizeConstants.Starch },
                { "sugar", FlowParticleSizeConstants.Sugar },
                { "coffee_mush", FlowParticleSizeConstants.CoffeeMush },
                { "contraceptive", FlowParticleSizeConstants.Contraceptive },
                { "zyanide", FlowParticleSizeConstants.Zyanide },
                { "nightmare_extract", FlowParticleSizeConstants.NightmareExtract },
                { "nightmare_cap_mush", FlowParticleSizeConstants.NightmareCapMush },
                { "chlorine", FlowParticleSizeConstants.Chlorine },
                { "synaprest", FlowParticleSizeConstants.Synaprest },
                { "fanta", FlowParticleSizeConstants.Fanta },
                { "methylol", FlowParticleSizeConstants.Methylol },
                { "reducing_agent", FlowParticleSizeConstants.ReducingAgent },
                { "acid", FlowParticleSizeConstants.Acid },
                { "base", FlowParticleSizeConstants.Base },
                { "stabilizing_agent", FlowParticleSizeConstants.StabilizingAgent },
                { "debris", FlowParticleSizeConstants.Debris },
                { "lipobond_agent", FlowParticleSizeConstants.LipobondAgent },
                { "livestock_steroid", FlowParticleSizeConstants.LivestockSteroid },
                { "livestock_immunity", FlowParticleSizeConstants.LivestockImmunity },
                { "wine_extract", FlowParticleSizeConstants.WineExtract },
                { "nomore", FlowParticleSizeConstants.Nomore },
            };
        }
    }
}
