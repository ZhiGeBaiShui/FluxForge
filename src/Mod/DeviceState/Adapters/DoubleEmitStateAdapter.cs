namespace FluxForge.Mod
{
    /// <summary>双出口装置没有持久化状态，适配器只提供统一入口。</summary>
    [DeviceAdapter("double_emit", typeof(DoubleEmitDevice))]
    public sealed class DoubleEmitStateAdapter : DeviceStateAdapter<DoubleEmitDevice>
    {
        public DoubleEmitStateAdapter(DoubleEmitDevice device)
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
