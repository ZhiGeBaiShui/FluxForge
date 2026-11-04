namespace FluxForge.Mod
{
    /// <summary>T 形分流装置没有持久化状态，适配器只提供统一入口。</summary>
    [DeviceAdapter("t_junction", typeof(TJunctionDevice))]
    public sealed class TJunctionStateAdapter : DeviceStateAdapter<TJunctionDevice>
    {
        public TJunctionStateAdapter(TJunctionDevice device)
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
