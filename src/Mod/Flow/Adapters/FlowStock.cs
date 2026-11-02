using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品 tag ↔ 流源属性的唯一读写点。
    ///
    /// 属性清单、槽位规则全部来自 <see cref="FlowTagMap"/>；
    /// 本类只做三件事：按 tag 遍历、按 tag 读写属性、按 tag 决定槽位。
    /// 不认识任何具体属性，也不区分"容器物品"与"普通物品"。
    /// </summary>
    public static class FlowStock
    {

        /// <summary>读出一份完整属性。</summary>
        public static FlowProperties Read(GameItem item)
        {
            FlowProperties properties = new FlowProperties();
            ReadInto(item, properties);
            return properties;
        }

        /// <summary>把 tag 里的值读进 <paramref name="target"/>（装置或一份属性）。</summary>
        public static void ReadInto(GameItem item, IFlowState target)
        {
            foreach (FlowPropertyKey key in target.Keys)
            {
                FlowTagMap.Binding binding = FlowTagMap.Get(key);
                target.Set(key, ReadNumber(item, binding));
            }
        }

        /// <summary>把 <paramref name="source"/> 的值写进物品 tag。</summary>
        public static void Write(GameItem item, IFlowState source)
        {
            foreach (FlowPropertyKey key in source.Keys)
            {
                FlowTagMap.Binding binding = FlowTagMap.Get(key);
                WriteNumber(item, binding, source.Get(key));
            }

            item.SyncModifiedState();
            Refresh(item);
        }

        /// <summary>总存量 = 一份属性里所有"相加类"属性之和。</summary>
        public static double Total(GameItem item)
        {
            return Read(item).SubstanceTotal;
        }

        /// <summary>
        /// tag 改完之后统一刷新一次外部表现。
        ///
        /// 容器：原版会按当前内容重算水质特性（纯度、显示名、价格、扫描结果、暴露状态）并刷新贴图；
        /// 我们的物品不在原版贴图白名单里时，贴图那步自然什么都不做。
        /// 普通物品：只标记贴图需要重取。
        /// </summary>
        private static void Refresh(GameItem item)
        {
            if (item.IsTag(FlowTags.LiquidContainerTag))
            {
                WaterFeatureHelper.OnLiquidContainerPourOutOf(item);
                SpriteHelper.UpdateWaterContainerSprite(item);
                return;
            }

            // spriteChanged 是 protected；SetSprite 是公开的“标记贴图需要重取”入口。
            item.SetSprite(item.spriteAtlasPath, item.spritePath);
        }

        /// <summary>从 modifiedState 读一个 tag 的数值；tag 不存在时按 0 处理（如新容器还没写过温度）。</summary>
        private static double ReadNumber(GameItem item, FlowTagMap.Binding binding)
        {
            if (!item.modifiedState.dict.TryGetValue(binding.Tag, out TagState state))
            {
                return 0d;
            }

            switch (binding.Slot)
            {
                case FlowTagMap.SlotKind.Double:
                    return state.GetDouble();
                default:
                    return state.GetInt();
            }
        }

        /// <summary>
        /// 按 tag 决定写哪个槽位；tag 是活的 TagState，写它才生效。
        ///
        /// tag 还不存在且要写的值是 0 时直接跳过——零值不必占一个 tag，
        /// 否则冰块 / 收集器上会凭空多出三十多个液体零 tag。
        /// 写非零值前先 Enable（与游戏写入方一致）；零值不碰 Enable，
        /// 避免 Take 把已有的零 tag 变成 enabled。
        /// </summary>
        private static void WriteNumber(GameItem item, FlowTagMap.Binding binding, double value)
        {
            bool zero = IsZero(binding.Slot, value);

            TagState state;
            if (!item.state.dict.TryGetValue(binding.Tag, out state))
            {
                if (zero)
                {
                    return;
                }

                state = item.state.GetTag(binding.Tag);
            }

            if (!zero)
            {
                state.Enable();
            }

            switch (binding.Slot)
            {
                case FlowTagMap.SlotKind.Double:
                    state.SetDouble(value);
                    break;
                default:
                    state.SetInt((int)value);
                    break;
            }
        }

        /// <summary>按槽位量化后是否为 0：int 会被截断，不能统一按 double 判。</summary>
        private static bool IsZero(FlowTagMap.SlotKind slot, double value)
        {
            return slot == FlowTagMap.SlotKind.Int ? (int)value == 0 : value == 0d;
        }
    }
}
