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
        /// <summary>表的一行：一个流源属性在物品 tag 上的落点，以及容器侧的取值方式。</summary>
        internal sealed class AttributeBinding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

            /// <summary>true = int 槽，false = float 槽。</summary>
            public bool IntSlot;

            /// <summary>需要实现的接口：装置与一份 <see cref="FlowProperties"/> 都要实现它。</summary>
            public Type Required;

            /// <summary>从装置或 <see cref="FlowProperties"/> 读数。</summary>
            public Func<object, double> Get;

            /// <summary>写回装置或 <see cref="FlowProperties"/>。</summary>
            public Action<object, double> Set;

            /// <summary>原版容器侧怎么取这个值（由 <see cref="FlowStock"/> 提供）。</summary>
            public Func<Il2Cpp.GameItem, double> FromContainer;
        }

        /// <summary>所有装置的同名属性共用一个 tag。</summary>
        private static readonly AttributeBinding[] _bindings =
        {
            new AttributeBinding
            {
                Tag = FlowTags.Stock,
                IntSlot = true,
                Required = typeof(IMoistureState),
                Get = target => ((IMoistureState)target).Moisture,
                Set = (target, value) => ((IMoistureState)target).Moisture = value,
                // 容器存量 = 所有物质之和。和 Tag 是同一个概念，但容器可能装多种物质，
                // 只读 CURRENT_PART_WATER 会漏掉非水液体。
                FromContainer = FlowStock.ReadContainerVolume,
            },
            new AttributeBinding
            {
                Tag = FlowTags.FlowTemperature,
                IntSlot = false,
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
