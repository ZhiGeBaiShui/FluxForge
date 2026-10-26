using System;


namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 液体容器存量的读写适配器。
    ///
    /// 发射装置本身就是一个**原版液体容器**（由 <c>WaterHelper.InitLiquidContainerItem</c> 装配），
    /// 所以「水从哪里来」这件事完全交给游戏：玩家把水瓶拖上去，原版挂在该物品上的
    /// target 委托会自己把每种物质的数量倒进来，温度也一并处理。
    ///
    /// 本类只负责两件游戏没提供的事：
    ///   ① 读总存量（游戏给了 <c>WaterHelper.GetTotalVolume</c>，直接用）
    ///   ② 发射时按量扣减（游戏只有「加」，没有「扣」，这里补一个）
    ///
    /// 这样框架层 <see cref="EmitterDevice"/> 完全不知道物品的存在，
    /// 只在激活时被告知「你有多少存量」，耦合点收敛在这一个类里。
    /// </summary>
    public static class LiquidStock
    {

        /// <summary>容器里所有物质的总量（单位与游戏内部一致，1/1000 mL）。</summary>
        public static double Total(Il2Cpp.GameItem item)
        {
            return Il2Cpp.WaterHelper.GetTotalVolume(item);
        }

        /// <summary>
        /// 扣减指定量的物质，返回实际扣走的量。
        ///
        /// 前置条件：<paramref name="item"/> 非空且 <paramref name="amount"/> &gt; 0。
        /// 唯一调用点是 <see cref="FlowRuntime"/> 的发射流程：它先确认容器里
        /// <c>Total(item) &gt; 0</c>，再把同一个非零值传进来，所以这里不必重复检查。
        ///
        /// 直接调原版 <c>WaterHelper.Remove</c>：它按各物质占比分摊、自动钳到 0，
        /// 并刷新水质与容器贴图。自己遍历 tag 改数值是错的——
        /// <c>GameItem.GetTagReadonly</c> 返回的是 <c>TagState.Clone()</c> 出来的副本，
        /// 改副本对活 tag 没有任何影响（这就是水量一直不掉的原因）。
        /// </summary>
        public static double Consume(Il2Cpp.GameItem item, double amount)
        {
            int before = Il2Cpp.WaterHelper.GetTotalVolume(item);
            int take = (int)Math.Min(before, amount);

            Il2Cpp.WaterHelper.Remove(item, take);
            return before - Il2Cpp.WaterHelper.GetTotalVolume(item);
        }

        /// <summary>
        /// 容器当前温度。原版写在 <c>CONTAINER_TEMP_TAG</c> 上（由 HandleReceiveLiquidTemp 维护），
        /// 这个 tag 的槽位类型没有明确文档，先按 double 读、为 0 再试 float，
        /// 都读不到就退回调用方给的默认值。
        ///
        /// 前置条件：<paramref name="item"/> 非空（发射入口已确认当前物品有效）。
        /// </summary>
        public static double Temperature(Il2Cpp.GameItem item, double fallback)
        {
            Il2Cpp.TagState state = item.GetTagReadonly("CONTAINER_TEMP_TAG");
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
