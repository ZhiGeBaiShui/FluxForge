namespace FluxForge.Mod
{
    /// <summary>
    /// 物品渲染层 tag：决定这件物品的视觉贴图是否要覆盖在流源层之上。
    ///
    /// 只描述“实例怎么渲染”，不负责贴图加载；贴图资源仍由 sprites.json / items.json 决定。
    /// </summary>
    public static class FlowRenderTags
    {
        public const string AboveFlow = "FLUXFORGE_RENDER_ABOVE_FLOW";

        public static bool IsAboveFlow(GameItem item)
        {
            if (item == null)
            {
                return false;
            }

            TagState tag;
            if (!item.state.dict.TryGetValue(AboveFlow, out tag))
            {
                return false;
            }

            return tag.GetInt() != 0;
        }
    }
}
