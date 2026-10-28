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
        /// 表的一行：一个流源属性落在哪个 tag，以及怎么在对象上取/放它。
        /// 槽位不在这里——它按 tag 集中登记（见 <see cref="SlotOf"/>），默认 int。
        /// </summary>
        internal sealed class AttributeBinding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

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

        /// <summary>
        /// 非 int 槽的 tag。游戏里数值型 tag 绝大多数是 int（计数、容量、含量），
        /// 需要别的槽位时在这里登记一行，没登记的一律按 int 读写。
        /// </summary>
        private static readonly Dictionary<string, SlotKind> _slots
            = new Dictionary<string, SlotKind>(StringComparer.Ordinal)
            {
                // 原版在 HandleReceiveLiquidTemp / 冰箱制冷里用 double 写它
                { FlowTags.ContainerTemperature, SlotKind.Double },
            };

        /// <summary>这个 tag 读写哪个槽位；没登记就是 int。</summary>
        public static SlotKind SlotOf(string tag)
        {
            SlotKind slot;
            return _slots.TryGetValue(tag, out slot) ? slot : SlotKind.Int;
        }

        /// <summary>所有装置的同名属性共用一个 tag。</summary>
        private static readonly AttributeBinding[] _bindings =
        {
            new AttributeBinding
            {
                Tag = FlowTags.Stock,
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
