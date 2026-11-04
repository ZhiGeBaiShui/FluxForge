namespace FluxForge.Mod
{
    /// <summary>分流装置没有持久化状态，适配器只提供统一入口。</summary>
    public sealed class SplitStateAdapter : IDeviceStateAdapter
    {
        private readonly SplitDevice _device;

        public SplitStateAdapter(SplitDevice device)
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
