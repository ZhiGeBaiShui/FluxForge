namespace FluxForge.Mod
{
    /// <summary>转向装置没有持久化状态，适配器只提供统一入口。</summary>
    [DeviceAdapter("turn", typeof(TurnDevice))]
    public sealed class TurnStateAdapter : DeviceStateAdapter<TurnDevice>
    {
        public TurnStateAdapter(TurnDevice device)
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
