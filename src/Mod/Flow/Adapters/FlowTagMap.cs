using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 我们管理的 tag 清单，以及"tag ↔ 装置成员 ↔ 槽位"的对应关系。
    ///
    /// 一行登记一个属性的全部信息：落在哪个 tag、由哪个单成员接口描述、读写哪个数值槽。
    /// 读写通过登记的强类型委托完成，不做反射——Mono 下这既更快，也让能力声明保持为空。
    ///
    /// 新增属性的步骤：
    ///   ① 在 Logic 加一个单成员接口，并让相关装置实现它；
    ///   ② 这里加一行（Tag / Getter / Setter / Slot）；
    ///   ③ items.json 声明该 tag 与对应的 statusLine。
    /// </summary>
    internal static class FlowTagMap
    {
        /// <summary>TagState 的数值槽位。原版不留类型元数据，槽位由写入方约定。</summary>
        internal enum SlotKind
        {
            Int,
            Float,
            Double,
        }

        /// <summary>一条映射：tag 名 + 读写该属性的委托 + 槽位。</summary>
        internal sealed class Binding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

            /// <summary>
            /// 读写哪个槽位。必须和原版写入方一致：
            /// 原版用 SetInt 写的 tag 这里就是 Int，用 double 写的就是 Double。
            /// </summary>
            public SlotKind Slot;

            /// <summary>从装置 / FlowProperties 读出属性值。</summary>
            public Func<object, double> Getter;

            /// <summary>把属性值写回装置 / FlowProperties。</summary>
            public Action<object, double> Setter;
        }

        /// <summary>我们管理的属性；顺序即遍历顺序。</summary>
        private static readonly Binding[] _bindings =
        {
            // 原版 AddLiquid / GetTotalVolume 都按 int 读写
            new Binding
            {
                Tag = FlowTags.CurrentPartWater,
                Slot = SlotKind.Int,
                Getter = target => ((IWaterState)target).Water,
                Setter = (target, value) => ((IWaterState)target).Water = value,
            },

            // 原版 HandleReceiveLiquidTemp / 冰箱制冷按 double 读写
            new Binding
            {
                Tag = FlowTags.ContainerTempTag,
                Slot = SlotKind.Double,
                Getter = target => ((ITemperatureState)target).Temperature,
                Setter = (target, value) => ((ITemperatureState)target).Temperature = value,
            },
        };

        /// <summary>只读视图：<see cref="FlowStock"/> 遍历用。</summary>
        internal static IReadOnlyList<Binding> Bindings
        {
            get { return _bindings; }
        }

        /// <summary>按 tag 从装置（或一份属性）上读出对应成员。</summary>
        internal static double Get(object target, string tag)
        {
            return PropertyOf(tag).Getter(target);
        }

        /// <summary>按 tag 把值写回装置（或一份属性）的对应成员。</summary>
        internal static void Set(object target, string tag, double value)
        {
            PropertyOf(tag).Setter(target, value);
        }

        private static Binding PropertyOf(string tag)
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Tag == tag)
                {
                    return binding;
                }
            }

            throw new ArgumentException("没有登记该 tag 的成员：" + tag, nameof(tag));
        }
    }
}
