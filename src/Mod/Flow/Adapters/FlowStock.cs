using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品流源存量的唯一适配点。存量有两种形态，对调用方透明：
    ///
    ///   ① **原版液体容器**：游戏用 <c>CURRENT_PART_*</c> / <c>CONTAINER_TEMP_TAG</c> 维护
    ///      （由 <see cref="FlowIntakeAttachment"/> 调 <c>InitLiquidContainerItem</c> 建立）；
    ///   ② **我们自己的存量**：物品 tag（由 <see cref="FlowTransfer"/> 写入）。
    ///
    /// 属性名字、tag 落点、槽位类型、容器侧取值全部来自 <see cref="FlowTagMap"/> 的表。
    /// 本类只负责"遍历表 + 在容器/物品两种来源之间选择"，不认识任何具体属性。
    /// </summary>
    public static class FlowStock
    {
        /// <summary>没有温度信息时的假定温度。</summary>
        public const double DefaultTemperature = 25d;

        /// <summary>这件物品是不是原版液体容器（按 tag 判定，与具体物品 id 无关）。</summary>
        public static bool IsContainer(Il2Cpp.GameItem item)
        {
            return item != null
                && item.Pointer != IntPtr.Zero
                && item.IsTag(FlowIntakeAttachment.ContainerTag);
        }

        /// <summary>按表读出一份属性：容器逐行 <c>FromContainer</c>，自己的存量逐行读 tag。</summary>
        public static FlowProperties Read(Il2Cpp.GameItem item)
        {
            bool container = IsContainer(item);
            FlowProperties properties = new FlowProperties(0d, 0d);

            foreach (FlowTagMap.AttributeBinding binding in FlowTagMap.Bindings)
            {
                double value = container
                    ? binding.FromContainer(item)
                    : ReadTag(item.state, binding);
                binding.Set(properties, value);
            }

            return properties;
        }

        /// <summary>总存量 = 一份属性里所有"相加类"属性之和。</summary>
        public static double Total(Il2Cpp.GameItem item)
        {
            return Read(item).SubstanceTotal;
        }

        /// <summary>
        /// 取走全部，返回取走前的属性。
        ///
        /// 容器走原版 <c>WaterHelper.Remove</c>：按物质占比分摊、自动钳到 0，并刷新水质与贴图。
        /// 自己的存量逐行写 0；温度等非存量属性随后由调用方按表写回。
        /// </summary>
        public static FlowProperties Take(Il2Cpp.GameItem item)
        {
            FlowProperties properties = Read(item);

            if (IsContainer(item))
            {
                Il2Cpp.WaterHelper.Remove(item, FlowTagMap.ToIntSlot(properties.SubstanceTotal));
                return properties;
            }

            foreach (FlowTagMap.AttributeBinding binding in FlowTagMap.Bindings)
            {
                if (binding.IntSlot)
                {
                    FlowTags.WriteInt(item.state, binding.Tag, 0);
                }
                else
                {
                    FlowTags.WriteFloat(item.state, binding.Tag, 0f);
                }
            }

            return properties;
        }

        private static double ReadTag(Il2Cpp.TagSystem state, FlowTagMap.AttributeBinding binding)
        {
            return binding.IntSlot
                ? FlowTags.ReadInt(state, binding.Tag, 0)
                : FlowTags.ReadFloat(state, binding.Tag, 0f);
        }
    }
}
