namespace FluxForge.Mod
{
    /// <summary>右下转向装置没有持久化状态，适配器只提供统一入口。</summary>
    [DeviceAdapter("l_right_down", typeof(LRightDownDevice))]
    public sealed class LRightDownStateAdapter : DeviceStateAdapter<LRightDownDevice>
    {
        public LRightDownStateAdapter(LRightDownDevice device)
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
