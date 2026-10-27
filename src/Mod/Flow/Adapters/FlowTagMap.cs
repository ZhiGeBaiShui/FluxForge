using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 装置状态 ↔ 物品 tag 的唯一映射表。
    ///
    /// 表按**属性**分行（水分 / 温度 / ...）。加一种新流源属性只需要：
    ///   ① 在 <see cref="FlowProperties"/> 加字段并声明分合规则；
    ///   ② 在 <see cref="Bindings"/> 加一行（tag / 槽位 / 接口 / 读写 / 容器侧取值）。
    /// 读写入口与 <see cref="FlowStock"/> 都只遍历这张表，不认识具体属性名，
    /// 也不按装置类型分支——所以新增装置仍然只要实现对应接口，本文件不用动。
    ///
    /// 不在这里做显示换算（µL → g 之类）——那是 items.json 的 statusLines 的事。
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

            /// <summary>原版容器侧怎么取这个值。</summary>
            public Func<Il2Cpp.GameItem, double> FromContainer;
        }

        /// <summary>
        /// 唯一的表。容器侧取值由各自的 <see cref="AttributeBinding.FromContainer"/> 负责，
        /// 具体实现放在 <see cref="FlowStock"/>（它才是"存量从哪来"的适配点）。
        /// </summary>
        private static readonly AttributeBinding[] _bindings =
        {
            new AttributeBinding
            {
                Tag = FlowTags.Stock,
                IntSlot = true,
                Required = typeof(IMoistureState),
                Get = target => ((IMoistureState)target).Moisture,
                Set = (target, value) => ((IMoistureState)target).Moisture = value,
                // 容器存量 = 所有物质之和。它和上面的 Tag 是同一个概念，但容器可能装多种物质，
                // 只读 CURRENT_PART_WATER 会漏掉非水液体，也会和 Take 的"按物质清空"对不上。
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

        /// <summary>只读视图：<see cref="FlowStock"/> 逐行读写时用，不要在外部改写。</summary>
        internal static IReadOnlyList<AttributeBinding> Bindings
        {
            get { return _bindings; }
        }

        /// <summary>
        /// target → tag：只写 target 实现了接口的那些行。
        ///
        /// 写必须用 <c>state.GetTag</c>（活引用）；<c>GetTagReadonly</c> 返回的是
        /// <c>TagState.Clone()</c> 副本，改它不会影响存档——这个坑踩过两次。
        /// </summary>
        public static void Write(Il2Cpp.GameItem item, object target)
        {
            foreach (AttributeBinding binding in _bindings)
            {
                if (!binding.Required.IsInstanceOfType(target))
                {
                    continue;
                }

                if (binding.IntSlot)
                {
                    item.state.GetTag(binding.Tag).valueInt = (int)binding.Get(target);
                }
                else
                {
                    item.state.GetTag(binding.Tag).valueFloat = (float)binding.Get(target);
                }
            }
        }

        /// <summary>tag → target：只写 target 实现了接口的那些行。</summary>
        public static void Read(Il2Cpp.GameItem item, object target)
        {
            foreach (AttributeBinding binding in _bindings)
            {
                if (!binding.Required.IsInstanceOfType(target))
                {
                    continue;
                }

                Il2Cpp.TagState state = item.GetTagReadonly(binding.Tag);
                binding.Set(
                    target,
                    binding.IntSlot ? state.valueInt : state.valueFloat);
            }
        }
    }
}
