using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品液体存量的唯一适配点。存量有两种形态，对调用方透明：
    ///
    ///   ① **原版液体容器**：游戏用 <c>CURRENT_PART_*</c> / <c>CONTAINER_TEMP_TAG</c> 维护
    ///      （由 <see cref="LiquidIntakeAttachment"/> 调 <c>InitLiquidContainerItem</c> 建立）；
    ///   ② **我们自己的存量**：<c>FLOW_STOCK</c> / <c>FLOW_TEMPERATURE</c>
    ///      （由 <see cref="LiquidTransfer"/> 写入）。
    ///
    /// 所以发射流程只问 Total / Temperature / Consume，不需要知道这件物品是哪一种。
    /// </summary>
    public static class LiquidStock
    {
        /// <summary>没有温度信息时的假定温度。</summary>
        public const double DefaultTemperature = 25d;

        private const string ContainerTemperatureTag = "CONTAINER_TEMP_TAG";

        /// <summary>这件物品是不是原版液体容器（按 tag 判定，与具体物品 id 无关）。</summary>
        public static bool IsContainer(Il2Cpp.GameItem item)
        {
            return item != null
                && item.Pointer != IntPtr.Zero
                && item.IsTag(LiquidIntakeAttachment.ContainerTag);
        }

        /// <summary>总存量。容器 = 各物质之和；自己的存量 = FLOW_STOCK。</summary>
        public static double Total(Il2Cpp.GameItem item)
        {
            return IsContainer(item)
                ? Il2Cpp.WaterHelper.GetTotalVolume(item)
                : FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
        }

        /// <summary>
        /// 扣减指定量，返回实际扣走的量。
        ///
        /// 容器走原版 <c>WaterHelper.Remove</c>：按物质占比分摊、自动钳到 0，并刷新水质与贴图。
        /// 自己的存量直接改活 tag 的 int 槽（<c>GetTagReadonly</c> 拿到的是副本，改它无效）。
        /// </summary>
        public static double Consume(Il2Cpp.GameItem item, double amount)
        {
            if (IsContainer(item))
            {
                int before = Il2Cpp.WaterHelper.GetTotalVolume(item);
                int take = (int)Math.Min(before, amount);

                Il2Cpp.WaterHelper.Remove(item, take);
                return before - Il2Cpp.WaterHelper.GetTotalVolume(item);
            }

            int stored = FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
            int taken = (int)Math.Min(stored, amount);

            FlowTags.WriteInt(item.state, FlowTags.Stock, stored - taken);
            return taken;
        }

        /// <summary>
        /// 当前温度。容器读 <c>CONTAINER_TEMP_TAG</c>（槽位类型未文档化，先按 double 再按 float）；
        /// 自己的存量读 <c>FLOW_TEMPERATURE</c>。都读不到就用调用方给的 fallback。
        /// </summary>
        public static double Temperature(Il2Cpp.GameItem item, double fallback)
        {
            if (!IsContainer(item))
            {
                return FlowTags.ReadFloat(item.state, FlowTags.FlowTemperature, (float)fallback);
            }

            Il2Cpp.TagState state = item.GetTagReadonly(ContainerTemperatureTag);
            if (state == null)
            {
                return fallback;
            }

            double asDouble = state.valueDouble;
            if (asDouble != 0d)
            {
                return asDouble;
            }

            double asFloat = state.valueFloat;
            return asFloat != 0d ? asFloat : fallback;
        }
    }
}
