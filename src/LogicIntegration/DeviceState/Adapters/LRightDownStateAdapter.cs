namespace FluxForge.Mod
{
    /// <summary>右下转向装置没有持久化状态：只负责创建，Load / Save 为空操作。</summary>
    [DeviceAdapter("l_right_down")]
    public sealed class LRightDownStateAdapter : DeviceStateAdapter<LRightDownDevice>
    {
        /// <summary>无参构造的无状态装置。</summary>
        protected override LRightDownDevice CreateDevice(GameItem item)
        {
            return new LRightDownDevice();
        }
    }
}
