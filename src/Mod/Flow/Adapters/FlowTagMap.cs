using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流源属性 → 物品 tag 的映射表（**纯数据，没有任何逻辑**）。
    ///
    /// 一行描述一个属性：落在哪个 tag、哪个槽位、需要哪一类接口、怎么取值写值、
    /// 以及"在原版容器里怎么取这个值"。读写的实现、容器与自有存量的选择、
    /// 所有 tag 落地，统一由 <see cref="FlowStock"/> 负责。
    ///
    /// 加一种新流源属性：
    ///   ① <see cref="FlowProperties"/> 加字段并声明分合规则；
    ///   ② 这里加一行。
    /// 其余代码不用动。
    /// </summary>
    internal static class FlowTagMap
    {
        /// <summary>
        /// 表的一行：一个流源属性落在哪个 tag、读写哪个槽位。
        /// 只有这两件事是这张表必须记住的——属性语义由它实现的接口决定。
        /// </summary>
        internal sealed class AttributeBinding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

            /// <summary>读写哪个数值槽（原版 TagState 没有类型信息，只能由调用方约定）。</summary>
            public SlotKind Slot;

            /// <summary>需要实现的接口：装置与一份 <see cref="FlowProperties"/> 都要实现它。</summary>
            public Type Required;

            /// <summary>从装置或 <see cref="FlowProperties"/> 读数。</summary>
            public Func<object, double> Get;

            /// <summary>写回装置或 <see cref="FlowProperties"/>。</summary>
            public Action<object, double> Set;

            /// <summary>原版容器侧怎么取这个值（由 <see cref="FlowStock"/> 提供）。</summary>
            public Func<Il2Cpp.GameItem, double> FromContainer;
        }

        /// <summary>TagState 的数值槽位。原版不留类型元数据，槽位由写入方约定。</summary>
        internal enum SlotKind
        {
            Int,
            Float,
            Double,
        }

        /// <summary>所有装置的同名属性共用一个 tag。</summary>
        private static readonly AttributeBinding[] _bindings =
        {
            new AttributeBinding
            {
                Tag = FlowTags.Stock,
                Slot = SlotKind.Int,
                Required = typeof(IMoistureState),
                Get = target => ((IMoistureState)target).Moisture,
                Set = (target, value) => ((IMoistureState)target).Moisture = value,
                // 容器里可能还有未建模的物质，总量口径 = 全部物质之和
                FromContainer = FlowStock.ReadContainerVolume,
            },
            new AttributeBinding
            {
                // 温度：整件物品一个值，直接落原版的容器温度 tag（double 槽），
                // 原版倒水、环境降温、冰箱制冷都写它。
                Tag = FlowTags.ContainerTemperature,
                Slot = SlotKind.Double,
                Required = typeof(ITemperatureState),
                Get = target => ((ITemperatureState)target).Temperature,
                Set = (target, value) => ((ITemperatureState)target).Temperature = value,
                FromContainer = FlowStock.ReadContainerTemperature,
            },
        };

        /// <summary>只读视图：<see cref="FlowStock"/> 遍历用。</summary>
        internal static IReadOnlyList<AttributeBinding> Bindings
        {
            get { return _bindings; }
        }
    }
}
