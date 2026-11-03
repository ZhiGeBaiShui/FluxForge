using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 我们管理的 tag 清单，以及"tag ↔ 流属性 ↔ 槽位"的对应关系。
    ///
    /// 清单分两部分：
    ///   · 液体：Logic 登记为 <see cref="FlowPropertyKind.Liquid"/> 的属性，
    ///     按原版 <c>CURRENT_PART_&lt;ID&gt;</c> 约定落成 <see cref="SlotKind.Int"/>；
    ///     清单从游戏 <c>Liquid.InitLiquid()</c> 手工同步。
    ///   · 标量：按 <c>&lt;ID&gt;_TAG</c> 约定生成（container_temp →
    ///     <c>CONTAINER_TEMP_TAG</c>），按 <see cref="SlotKind.Double"/> 存。
    /// tag 与槽位都是 mod 层的约定，由 <see cref="FlowPropertyKind"/> 分支选择。
    ///
    /// 液体 tag 由游戏 <c>InitLiquidContainerItem</c> 建立；items.json 只写需要初始化的
    /// 那几个（测试瓶各写一种液体，有状态的装置写 water + 温度），其余槽位保持默认 0。
    ///
    /// 新增属性的步骤：
    ///   ① 在 Logic 的属性表登记（id / 种类 / 分合规则）；
    ///   ② 液体 tag 由游戏创建；标量按 id 生成 &lt;ID&gt;_TAG，需要显示时在 items.json 声明 statusLine。
    /// </summary>
    internal static class FlowTagMap
    {
        /// <summary>TagState 的数值槽位。原版不留类型元数据，槽位由写入方约定。</summary>
        internal enum SlotKind
        {
            Int,
            Double,
        }

        /// <summary>一条映射：tag 名 + 该 tag 对应的流属性 key + 槽位。</summary>
        internal sealed class Binding
        {
            /// <summary>落在物品上的 tag 名。</summary>
            public string Tag;

            /// <summary>该 tag 对应的流属性。</summary>
            public FlowPropertyKey Key;

            /// <summary>
            /// 读写哪个槽位。必须和原版写入方一致：
            /// 原版用 SetInt 写的 tag 这里就是 Int，用 double 写的就是 Double。
            /// </summary>
            public SlotKind Slot;
        }

        /// <summary>我们管理的属性；顺序即遍历顺序（与 Logic 的属性表一致）。</summary>
        private static readonly Binding[] _bindings = Build();

        /// <summary>只读视图：<see cref="FlowStock"/> 遍历用。</summary>
        internal static IReadOnlyList<Binding> Bindings
        {
            get { return _bindings; }
        }

        /// <summary>按属性键取对应 binding；键必须已登记。</summary>
        internal static Binding Get(FlowPropertyKey key)
        {
            return _bindings[key.Index - 1];
        }

        /// <summary>
        /// 一个循环按 Kind 生成全部映射：
        ///   · 液体：<c>CURRENT_PART_&lt;ID&gt;</c> + Int（原版 currentPart 全是 int）；
        ///   · 标量：<c>&lt;ID&gt;_TAG</c> + Double（container_temp → CONTAINER_TEMP_TAG）。
        /// </summary>
        private static Binding[] Build()
        {
            List<Binding> bindings = new List<Binding>(FlowPropertyRegistry.Count);

            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                bool liquid = def.Kind == FlowPropertyKind.Liquid;

                bindings.Add(new Binding
                {
                    Tag = liquid
                        ? "CURRENT_PART_" + def.Id.ToUpperInvariant()
                        : def.Id.ToUpperInvariant() + "_TAG",
                    Key = def.Key,
                    Slot = liquid ? SlotKind.Int : SlotKind.Double,
                });
            }

            return bindings.ToArray();
        }
    }
}
