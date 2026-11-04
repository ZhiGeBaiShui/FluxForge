namespace FluxForge.Mod
{
    /// <summary>
    /// 物品身份 / 锚点等非流源属性的 tag 名；常量名与 tag 英文名一一对应，方便按名字搜到落点。
    ///
    /// 流源属性 tag 不在这里列：它们由 <see cref="FlowTagNaming.ForProperty"/>
    /// 按注册表 id + 种类生成。液体 tag 由游戏 <c>InitLiquidContainerItem</c>
    /// 按 <c>CURRENT_PART_&lt;ID&gt;</c> 创建，标量按 <c>&lt;ID&gt;_TAG</c>。
    /// </summary>
    public static class FlowTags
    {
        /// <summary>
        /// 原版液体容器的身份 tag
        /// （flow_container / water_bottle 装配件经 <c>WaterHelper.InitLiquidContainerItem</c> 建立）。
        /// </summary>
        public const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";

        /// <summary>
        /// 中心格的局部 X 坐标。items.json 的 <c>anchorX</c> 写入这里。
        ///
        /// 名字带 DEVICE 是历史原因；它描述的是「物品的中心格」，
        /// 装置和发射器共用同一套 anchor，不区分角色。
        /// </summary>
        public const string AnchorX = "FLUXFORGE_DEVICE_ANCHOR_X";

        /// <summary>中心格的局部 Y 坐标，与 <see cref="AnchorX"/> 成对使用。</summary>
        public const string AnchorY = "FLUXFORGE_DEVICE_ANCHOR_Y";
    }
}
