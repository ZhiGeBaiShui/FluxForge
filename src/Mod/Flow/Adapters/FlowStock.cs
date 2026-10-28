using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品 tag ↔ 流源属性的唯一读写点。
    ///
    /// 属性清单、槽位规则全部来自 <see cref="FlowTagMap"/>；
    /// 本类只做三件事：按 tag 遍历、按 tag 读写成员、按 tag 决定槽位。
    /// 不认识任何具体属性，也不区分"容器物品"与"普通物品"。
    /// </summary>
    public static class FlowStock
    {

        /// <summary>读出一份完整属性。</summary>
        public static FlowProperties Read(Il2Cpp.GameItem item)
        {
            FlowProperties properties = new FlowProperties(0d, 0d);
            ReadInto(item, properties);
            return properties;
        }

        /// <summary>把 tag 里的值读进 <paramref name="target"/>（装置或一份属性）。</summary>
        public static void ReadInto(Il2Cpp.GameItem item, object target)
        {
            foreach (FlowTagMap.Binding binding in FlowTagMap.Bindings)
            {
                FlowTagMap.Set(target, binding.Tag, ReadNumber(item, binding));
            }
        }

        /// <summary>把 <paramref name="target"/> 的值写进物品 tag。</summary>
        public static void Write(Il2Cpp.GameItem item, object target)
        {
            foreach (FlowTagMap.Binding binding in FlowTagMap.Bindings)
            {
                WriteNumber(item, binding, FlowTagMap.Get(target, binding.Tag));
            }

            Refresh(item);
        }

        /// <summary>总存量 = 一份属性里所有"相加类"属性之和。</summary>
        public static double Total(Il2Cpp.GameItem item)
        {
            return Read(item).SubstanceTotal;
        }

        /// <summary>
        /// 取走全部：按映射表把每个属性对应的 tag 写 0，返回取走前的属性。
        /// 容器与普通物品走同一条路——tag 就是唯一真相。
        /// </summary>
        public static FlowProperties Take(Il2Cpp.GameItem item)
        {
            FlowProperties properties = Read(item);

            foreach (FlowTagMap.Binding binding in FlowTagMap.Bindings)
            {
                WriteNumber(item, binding, 0d);
            }

            Refresh(item);
            return properties;
        }

        /// <summary>
        /// tag 改完之后统一刷新一次外部表现。
        ///
        /// 容器：原版会按当前内容重算水质特性（纯度、显示名、价格、扫描结果、暴露状态）并刷新贴图；
        /// 我们的物品不在原版贴图白名单里时，贴图那步自然什么都不做。
        /// 普通物品：只标记贴图需要重取。
        /// </summary>
        private static void Refresh(Il2Cpp.GameItem item)
        {
            if (item.IsTag(FlowTags.LiquidContainerTag))
            {
                Il2Cpp.WaterFeatureHelper.OnLiquidContainerPourOutOf(item);
                Il2Cpp.SpriteHelper.UpdateWaterContainerSprite(item);
                return;
            }

            item.spriteChanged = true;
        }

        /// <summary>读一个 tag 的数值；tag 不存在时按 0 处理（如新容器还没写过温度）。</summary>
        private static double ReadNumber(Il2Cpp.GameItem item, FlowTagMap.Binding binding)
        {
            Il2Cpp.TagState state = item.GetTagReadonly(binding.Tag);
            if (state == null)
            {
                return 0d;
            }

            switch (binding.Slot)
            {
                case FlowTagMap.SlotKind.Double:
                    return state.GetDouble();
                case FlowTagMap.SlotKind.Float:
                    return state.GetFloat();
                default:
                    return state.GetInt();
            }
        }

        /// <summary>按 tag 决定写哪个槽位；tag 是活的 TagState，写它才生效。</summary>
        private static void WriteNumber(Il2Cpp.GameItem item, FlowTagMap.Binding binding, double value)
        {
            Il2Cpp.TagState state = item.state.GetTag(binding.Tag);

            switch (binding.Slot)
            {
                case FlowTagMap.SlotKind.Double:
                    state.SetDouble(value);
                    break;
                case FlowTagMap.SlotKind.Float:
                    state.SetFloat((float)value);
                    break;
                default:
                    state.SetInt((int)value);
                    break;
            }
        }
    }
}
