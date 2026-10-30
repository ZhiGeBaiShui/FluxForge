namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 我们使用的 tag 名，常量名与 tag 英文名一一对应，方便按名字搜到落点。
    ///
    /// 这些字符串在 items.json 里也会出现（创建时的初始值、tooltip 的取值），
    /// 两边必须一致。
    /// </summary>
    internal static class FlowTags
    {
        /// <summary>水量 / 含量。原版按物质存储液体含量用的就是这一套 tag。</summary>
        public const string CurrentPartWater = "CURRENT_PART_WATER";


        /// <summary>原版液体容器的身份 tag（由 flow_container 装配件建立）。</summary>
        public const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";

        /// <summary>
        /// 容器内容物的温度（double 槽）。原版在倒水（TransferLiquid / AddLiquid）、
        /// 环境降温、冰箱制冷时都会写它。
        /// </summary>
        public const string ContainerTempTag = "CONTAINER_TEMP_TAG";
    }
}
