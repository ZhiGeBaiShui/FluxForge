namespace FluxForge.Mod
{
    /// <summary>转向装置没有持久化状态：只负责创建，Load / Save 为空操作。</summary>
    [DeviceAdapter("turn")]
    public sealed class TurnStateAdapter : DeviceStateAdapter<TurnDevice>
    {
        /// <summary>无参构造的无状态装置。</summary>
        protected override TurnDevice CreateDevice(GameItem item)
        {
            return new TurnDevice();
        }
    }
}
