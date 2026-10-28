namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 流源属性在物品 tag 上的名字。读写直接用原版 API：
    ///   · 读：<c>item.GetTagReadonly(name)</c>（返回副本，只读用）
    ///   · 写：<c>item.state.GetTag(name)</c>（活的 TagState，改字段才生效）
    ///
    /// 名字集中在这里，是因为 items.json 里也写着同样的字符串，两边必须一致。
    /// </summary>
    internal static class FlowTags
    {
        /// <summary>存量。绑定原版的水分 tag —— 水分本来就是游戏的概念，不自造同义 tag。</summary>
        public const string Stock = "CURRENT_PART_WATER";

        /// <summary>原版液体容器的身份 tag（由 flow_container 装配件建立）。</summary>
        public const string Container = "LIQUID_CONTAINER_TAG";

        /// <summary>
        /// 温度：直接绑定原版的容器温度 tag（double 槽）。
        /// 原版在倒水（TransferLiquid/AddLiquid）、环境降温、冰箱制冷时都会写它，
        /// 我们只读不写——写温度是原版的职责。
        /// </summary>
        public const string ContainerTemperature = "CONTAINER_TEMP_TAG";
    }
}
