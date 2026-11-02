using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 流源属性注册表：属性清单、槽位与分合规则的唯一来源。
    ///
    /// 顺序即槽位顺序，按游戏 <c>Liquid.InitLiquid()</c> 的液体登记顺序排列，
    /// 最后是标量属性 temperature。Logic 只认识这里的稳定标识，
    /// 游戏 tag 的对应关系由 mod 层维护。
    ///
    /// 分合规则按种类统一：液体一律 Divide/Add，标量一律 Keep/WeightedAverage，
    /// 定义里不单独声明。新增 Scalar 必须在 Mod 层为它指定独立 tag。
    ///
    /// 新增属性的步骤：
    ///   ① 在 <see cref="Build"/> 的清单里追加一行；
    ///   ② 在 <see cref="FlowProps"/> 加同名槽位键；
    ///   ③ 新增标量时在 Mod 层指定独立 tag（FlowTagMap）。
    /// </summary>
    public static class FlowPropertyRegistry
    {
        private static readonly FlowPropertyDef[] _all = Build();
        private static readonly FlowPropertyKey[] _allKeys = BuildKeys();

        /// <summary>全部属性，下标与槽位一一对应（槽位 = 下标 + 1）。</summary>
        public static IReadOnlyList<FlowPropertyDef> All
        {
            get { return _all; }
        }

        /// <summary>全部属性键，顺序与 <see cref="All"/> 一致。</summary>
        public static IReadOnlyList<FlowPropertyKey> AllKeys
        {
            get { return _allKeys; }
        }

        /// <summary>属性个数（当前 36 种液体 + 温度 = 37）。</summary>
        public static int Count
        {
            get { return _all.Length; }
        }

        /// <summary>按稳定标识取定义；没有登记时抛 <see cref="ArgumentException"/>。</summary>
        public static FlowPropertyDef Get(string id)
        {
            foreach (FlowPropertyDef def in _all)
            {
                if (def.Id == id)
                {
                    return def;
                }
            }

            throw new ArgumentException("没有登记该流源属性：" + id, nameof(id));
        }

        /// <summary>按稳定标识取槽位键。</summary>
        public static FlowPropertyKey Key(string id)
        {
            return Get(id).Key;
        }

        private static FlowPropertyDef[] Build()
        {
            // 顺序必须与游戏 Liquid.InitLiquid() 的登记顺序一致。
            string[] liquids =
            {
                "water",
                "heavy_metal",
                "organic_waste",
                "microplastic",
                "microbe",
                "chemical_contaminant",
                "physical_contaminant",
                "mineral",
                "dream_cap_mush",
                "dream_cap_extract",
                "plant_matter",
                "alcohol",
                "caffeine",
                "protein",
                "fat",
                "starch",
                "sugar",
                "coffee_mush",
                "contraceptive",
                "zyanide",
                "nightmare_extract",
                "nightmare_cap_mush",
                "chlorine",
                "synaprest",
                "fanta",
                "methylol",
                "reducing_agent",
                "acid",
                "base",
                "stabilizing_agent",
                "debris",
                "lipobond_agent",
                "livestock_steroid",
                "livestock_immunity",
                "wine_extract",
                "nomore",
            };

            FlowPropertyDef[] defs = new FlowPropertyDef[liquids.Length + 1];
            for (int i = 0; i < liquids.Length; i++)
            {
                defs[i] = new FlowPropertyDef(
                    liquids[i], FlowPropertyKind.Liquid,
                    SplitRule.Divide, MergeRule.Add, i + 1);
            }

            defs[liquids.Length] = new FlowPropertyDef(
                "container_temp", FlowPropertyKind.Scalar,
                SplitRule.Keep, MergeRule.WeightedAverage, liquids.Length + 1);

            return defs;
        }

        private static FlowPropertyKey[] BuildKeys()
        {
            FlowPropertyKey[] keys = new FlowPropertyKey[_all.Length];
            for (int i = 0; i < _all.Length; i++)
            {
                keys[i] = _all[i].Key;
            }

            return keys;
        }
    }
}
