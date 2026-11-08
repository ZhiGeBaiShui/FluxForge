namespace FluxForge.Mod
{
    /// <summary>
    /// 装置状态回写。
    ///
    /// <see cref="IFlowNotifier.Changed"/> 立即触发 <see cref="Save"/>：
    /// 同一调用栈内完成 adapter.Save → tag → ItemTagCommit。
    /// 不做脏标记、不做帧末合并；装置状态与 tag 保持同步。
    /// </summary>
    public static class DeviceStateSync
    {
        /// <summary>装置 → tag；只要有任一适配器真的写了值，就统一提交。</summary>
        public static void Save(DeviceHost host)
        {
            if (!host.SaveAll())
            {
                return;
            }

            ItemTagCommit.Commit(host.Item);
        }

        /// <summary>装置耗尽：先同步回写，再注销并移出载体。</summary>
        public static void Exhausted(DeviceHost host)
        {
            Save(host);

            GameItem item = host.Item;
            DeviceRegistry.Unregister(item);
            item.parentInventory.Expel(item);
        }
    }
}
