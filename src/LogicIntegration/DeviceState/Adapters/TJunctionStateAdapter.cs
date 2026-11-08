namespace FluxForge.Mod
{
    /// <summary>T 形分流装置没有持久化状态：只负责创建，Load / Save 为空操作。</summary>
    [DeviceAdapter("t_junction")]
    public sealed class TJunctionStateAdapter : DeviceStateAdapter<TJunctionDevice>
    {
        /// <summary>无参构造的无状态装置。</summary>
        protected override TJunctionDevice CreateDevice(GameItem item)
        {
            return new TJunctionDevice();
        }
    }
}
