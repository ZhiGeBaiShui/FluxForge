namespace FluxForge.Mod
{
    /// <summary>
    /// 物品挂上容器时的装置绑定：遍历装置目录里的全部类型名，
    /// 命中哪个 tag 就用哪个类型构造装置，再用 tag 映射把状态写进装置，最后登记。
    /// </summary>
    internal static class ItemDeviceBinding
    {
        /// <summary>物品挂上容器时调用。重复调用安全——移动会反复进入容器。</summary>
        internal static void Bind(GameItem item)
        {
            if (DeviceRegistry.Contains(item))
            {
                return;
            }

            foreach (string kind in FlowDeviceCatalog.Keys)
            {
                if (!item.IsTag(kind))
                {
                    continue;
                }

                IFlowNotifier device = FlowDeviceCatalog.Create(kind);

                // 装置自己声明拥有哪些槽位：分流器声明空集合，这里是天然空操作。
                FlowStock.ReadInto(item, device);
                DeviceRegistry.Register(item, device);
                return;
            }
        }
    }
}
