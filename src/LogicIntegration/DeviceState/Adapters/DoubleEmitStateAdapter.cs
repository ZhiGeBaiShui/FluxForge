namespace FluxForge.Mod
{
    /// <summary>同口双发装置没有持久化状态：只负责创建，Load / Save 为空操作。</summary>
    [DeviceAdapter("double_emit")]
    public sealed class DoubleEmitStateAdapter : DeviceStateAdapter<DoubleEmitDevice>
    {
        /// <summary>无参构造的无状态装置。</summary>
        protected override DoubleEmitDevice CreateDevice(GameItem item)
        {
            return new DoubleEmitDevice();
        }
    }
}
