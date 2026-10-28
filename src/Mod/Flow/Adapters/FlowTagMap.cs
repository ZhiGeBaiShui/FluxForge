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
        /// 表的一行：一个流源属性在物品 tag 上的落点。
        ///
        /// 属性分两类，区别在 <see cref="PartId"/>：
        ///   · **物质属性**（水、有机废物…）：<c>PartId</c> 非空，落在原版
        ///     <c>CURRENT_PART_&lt;ID&gt;</c> 上，可分、可相加、可转移；
        ///   · **扩展属性**（温度…）：<c>PartId</c> 为空，用自己的 tag，是整件物品的单一值，
        ///     不按物质拆分——温度不能"取走一半"。
        /// </summary>
        internal sealed class AttributeBinding
        {
            /// <summary>物质 id（如 "water"）。非空 = 物质属性；空 = 扩展属性。</summary>
            public string PartId;

            /// <summary>
            /// 落在物品上的 tag 名。物质属性必须等于 <c>CURRENT_PART_&lt;PartId&gt;</c>；
            /// 扩展属性用 <see cref="FlowTags"/> 里自己的名字。
            /// </summary>
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

        /// <summary>所有装置的同名属性共用一个 tag。</summary>
        private static readonly AttributeBinding[] _bindings =
        {
            new AttributeBinding
            {
                PartId = "water",
                Tag = FlowTags.Stock,
                Required = typeof(IMoistureState),
                Get = target => ((IMoistureState)target).Moisture,
                Set = (target, value) => ((IMoistureState)target).Moisture = value,
                // 容器里可能还有未建模的物质，总量口径 = 全部物质之和
                FromContainer = FlowStock.ReadContainerVolume,
            },
            new AttributeBinding
            {
                // 温度不是物质：整件物品一个值，不参与"按物质分配"。
                // 直接落原版的容器温度 tag，原版倒水、环境降温、冰箱制冷都写它。
                PartId = null,
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
