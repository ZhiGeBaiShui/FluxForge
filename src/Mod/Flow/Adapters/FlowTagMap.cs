using System;
using System.Collections.Generic;
using System.Reflection;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 我们管理的 tag 清单，以及"tag ↔ 装置成员 ↔ 槽位"的对应关系。
    ///
    /// 一行登记一个属性的全部信息：落在哪个 tag、由哪个单成员接口描述、读写哪个数值槽。
    /// 取值/写值靠接口反射完成，不写死成员名。
    ///
    /// 新增属性的步骤：
    ///   ① 在 Logic 加一个单成员接口，并让相关装置实现它；
    ///   ② 这里加一行（Tag / Member / Slot）；
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

        /// <summary>一条映射：tag 名 + 描述该属性的单成员接口 + 槽位。</summary>
        internal sealed class Binding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

            /// <summary>只有一个 double 属性的接口（装置与 FlowProperties 都实现它）。</summary>
            public Type Member;

            /// <summary>
            /// 读写哪个槽位。必须和原版写入方一致：
            /// 原版用 SetInt 写的 tag 这里就是 Int，用 double 写的就是 Double。
            /// </summary>
            public SlotKind Slot;

            /// <summary>接口上那个唯一属性的反射句柄，启动时解析一次。</summary>
            internal PropertyInfo Property;
        }

        /// <summary>我们管理的属性；顺序即遍历顺序。</summary>
        private static readonly Binding[] _bindings =
        {
            // 原版 AddLiquid / GetTotalVolume 都按 int 读写
            new Binding
            {
                Tag = FlowTags.CurrentPartWater,
                Member = typeof(IMoistureState),
                Slot = SlotKind.Int,
            },

            // 原版 HandleReceiveLiquidTemp / 冰箱制冷按 double 读写
            new Binding
            {
                Tag = FlowTags.ContainerTempTag,
                Member = typeof(ITemperatureState),
                Slot = SlotKind.Double,
            },
        };

        static FlowTagMap()
        {
            foreach (Binding binding in _bindings)
            {
                binding.Property = binding.Member.GetProperties()[0];
            }
        }

        /// <summary>只读视图：<see cref="FlowStock"/> 遍历用。</summary>
        internal static IReadOnlyList<Binding> Bindings
        {
            get { return _bindings; }
        }

        /// <summary>按 tag 从装置（或一份属性）上读出对应成员。</summary>
        internal static double Get(object target, string tag)
        {
            return (double)PropertyOf(tag).GetValue(target);
        }

        /// <summary>按 tag 把值写回装置（或一份属性）的对应成员。</summary>
        internal static void Set(object target, string tag, double value)
        {
            PropertyOf(tag).SetValue(target, value);
        }

        private static PropertyInfo PropertyOf(string tag)
        {
            foreach (Binding binding in _bindings)
            {
                if (binding.Tag == tag)
                {
                    return binding.Property;
                }
            }

            throw new ArgumentException("没有登记该 tag 的成员：" + tag, nameof(tag));
        }
    }
}
