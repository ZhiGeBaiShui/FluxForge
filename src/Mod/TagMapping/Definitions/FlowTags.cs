namespace FluxForge.Mod
{
    /// <summary>
    /// 我们使用的 tag 名，常量名与 tag 英文名一一对应，方便按名字搜到落点。
    ///
    /// items.json 里出现的 tag 必须与 <see cref="FlowTagMap"/> 生成的一致
    /// （创建时的初始值、tooltip 的取值）。
    /// 液体 tag 不在这里列：它们由游戏 <c>InitLiquidContainerItem</c> 按
    /// <c>CURRENT_PART_&lt;ID&gt;</c> 创建，清单见 <see cref="FlowTagMap"/>。
    /// </summary>
    internal static class FlowTags
    {
        /// <summary>原版液体容器的身份 tag（由 flow_container 装配件建立）。</summary>
        public const string LiquidContainerTag = "LIQUID_CONTAINER_TAG";
    }
}
