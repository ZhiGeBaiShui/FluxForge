namespace FluxForge.Mod
{
    /// <summary>过滤装置没有持久化状态，适配器只提供统一入口。</summary>
    [DeviceAdapter("filter", typeof(FilterDevice))]
    public sealed class FilterStateAdapter : DeviceStateAdapter<FilterDevice>
    {
        public FilterStateAdapter(FilterDevice device)
            : base(device)
        {
        }

        public override void Load(ITagStore tags)
        {
        }

        public override bool Save(ITagStore tags)
        {
            return false;
        }
    }
}
