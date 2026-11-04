namespace FluxForge.Mod
{
    /// <summary>
    /// 物品 tag 写入后的统一提交：先重建 modifiedState，再刷新外部表现。
    ///
    /// 流源写入（<see cref="FlowStock.Write"/>）和装置状态回写（DeviceStateSync）
    /// 都只负责改 <c>state</c>，最终都调用这里；调用方不再自己拼
    /// <c>SyncModifiedState</c> + 表现刷新。
    /// </summary>
    public static class ItemTagCommit
    {
        public static void Commit(GameItem item)
        {
            item.SyncModifiedState();

            if (item.IsTag(FlowTags.LiquidContainerTag))
            {
                WaterFeatureHelper.OnLiquidContainerPourOutOf(item);
                SpriteHelper.UpdateWaterContainerSprite(item);
                return;
            }

            // spriteChanged 是 protected；SetSprite 是公开的“标记贴图需要重取”入口。
            item.SetSprite(item.spriteAtlasPath, item.spritePath);
        }
    }
}
