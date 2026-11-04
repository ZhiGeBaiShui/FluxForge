namespace FluxForge.Mod
{
    /// <summary>过滤装置没有持久化状态，适配器只提供统一入口。</summary>
    public sealed class FilterStateAdapter : IDeviceStateAdapter
    {
        private readonly FilterDevice _device;

        public FilterStateAdapter(FilterDevice device)
        {
            _device = device;
        }

        public object Device
        {
            get { return _device; }
        }

        public void Load(ITagStore tags)
        {
        }

        public bool Save(ITagStore tags)
        {
            return false;
        }
    }
}
