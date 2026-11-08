namespace FluxForge.Mod
{
    /// <summary>
    /// 装置目录里的一条定义：key + 创建 / 读写的单例适配器。
    ///
    /// 定义由 <see cref="DeviceAdapterCatalog"/> 在启动期扫描生成后不再变化；
    /// Host 直接保存 Host 侧的绑定，不会按 key 二次查表。
    /// </summary>
    public sealed class DeviceDefinition
    {
        public DeviceDefinition(string key, DeviceStateAdapter adapter)
        {
            Key = key;
            Adapter = adapter;
        }

        /// <summary>装置身份（items.json 的 device 字段 == 物品 tag 名）。</summary>
        public string Key { get; }

        /// <summary>本品类共用的无状态适配器单例。</summary>
        public DeviceStateAdapter Adapter { get; }
    }
}
