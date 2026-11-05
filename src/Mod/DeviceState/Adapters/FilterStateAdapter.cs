namespace FluxForge.Mod
{
    /// <summary>过滤装置没有持久化状态：只负责创建，Load / Save 为空操作。</summary>
    [DeviceAdapter("filter")]
    public sealed class FilterStateAdapter : DeviceStateAdapter<FilterDevice>
    {
        /// <summary>无参构造的无状态装置。</summary>
        protected override FilterDevice CreateDevice(GameItem item)
        {
            return new FilterDevice();
        }
    }
}
