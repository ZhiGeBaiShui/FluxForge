using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 离心机：按进入速度选择可分离粒径，再把可分离成分按固定系数 k 分到重相。
    ///
    /// 一次通过：
    ///   x = 粒径 >= threshold 的可分离总量
    ///   y = 其余液体总量
    ///   x1 = max(0, x - k*y)
    ///
    /// 重相 = x1（可分离成分按比例分配）
    /// 轻相 = 剩余可分离成分 + 全部不可分离成分
    ///
    /// 双出口：轻相继续走 light 方向，重相走 heavy 方向。
    /// 不判断流源从哪个方向进入，也不改变速度。
    /// </summary>
    public sealed class CentrifugeDevice : IFlowRouter
    {
        /// <summary>重相出口。</summary>
        private readonly int _heavyDirection;

        /// <summary>轻相出口。</summary>
        private readonly int _lightDirection;

        /// <summary>逻辑粒径；温度等标量不参与。</summary>
        private static readonly Dictionary<string, double> ParticleSizes
            = BuildParticleSizes();

        /// <summary>固定分离系数；越大越偏向保留轻相。</summary>
        public double K { get; }

        public CentrifugeDevice(
            double k,
            int lightDirection,
            int heavyDirection)
        {
            if (k < 1d || k > 10d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(k), "离心机系数 k 必须在 1～10 之间");
            }

            ValidateDirection(lightDirection, nameof(lightDirection));
            ValidateDirection(heavyDirection, nameof(heavyDirection));

            if (lightDirection == heavyDirection)
            {
                throw new ArgumentException("轻相和重相出口不能相同");
            }

            K = k;
            _lightDirection = lightDirection;
            _heavyDirection = heavyDirection;
        }

        /// <inheritdoc/>
        public bool Route(
            FlowEntity incoming,
            FlowSpawner flows,
            int cellX,
            int cellY)
        {
            double speed = incoming.Motion == null ? 0d : incoming.Motion.Speed;
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

                double amount = incoming.Properties.Get(def.Key);
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

            double removable = separable - K * nonSeparable;
            if (removable <= 0d || separable <= 0d)
            {
                // 没有足够的可分离物可以带走，原流源正常通过。
                return false;
            }

            double heavyRatio = removable / separable;

            FlowProperties heavy = new FlowProperties();
            FlowProperties light = new FlowProperties();

            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                double amount = incoming.Properties.Get(def.Key);

                if (def.Kind != FlowPropertyKind.Liquid)
                {
                    // 标量按 Keep 处理，两相都保留原值。
                    heavy.Set(def.Key, amount);
                    light.Set(def.Key, amount);
                    continue;
                }

                if (amount > 0d
                    && IsSeparable(def.Id, threshold))
                {
                    double heavyAmount = amount * heavyRatio;
                    heavy.Set(def.Key, heavyAmount);
                    light.Set(def.Key, amount - heavyAmount);
                }
                else
                {
                    light.Set(def.Key, amount);
                }
            }

            flows.Kill(incoming);

            double outputSpeed = incoming.Motion == null
                ? 0d
                : incoming.Motion.Speed;

            Spawn(
                flows,
                cellX,
                cellY,
                _heavyDirection,
                outputSpeed,
                heavy);

            Spawn(
                flows,
                cellX,
                cellY,
                _lightDirection,
                outputSpeed,
                light);

            return true;
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

        private static void Spawn(
            FlowSpawner flows,
            int cellX,
            int cellY,
            int direction,
            double speed,
            FlowProperties properties)
        {
            if (properties.SubstanceTotal <= 0d)
            {
                return;
            }

            // 输出方向由装置的两个出口决定；速度大小保持不变。
            FlowEntity entity = new FlowEntity(
                new FlowPos(cellX, cellY),
                new FlowMotionData(direction, speed),
                properties);

            flows.Accept(entity);
        }

        private static bool IsSeparable(string id, double threshold)
        {
            double size;
            return ParticleSizes.TryGetValue(id, out size)
                && size >= threshold;
        }

        private static void ValidateDirection(int direction, string paramName)
        {
            if (direction < 0 || direction > 3)
            {
                throw new ArgumentOutOfRangeException(
                    paramName, "方向必须是 0～3");
            }
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
