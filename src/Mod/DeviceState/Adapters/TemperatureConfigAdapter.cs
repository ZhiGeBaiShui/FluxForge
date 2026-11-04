namespace FluxForge.Mod
{
    /// <summary>温度控制装置适配器：只从 tag 读取初始温度配置。</summary>
    [DeviceAdapter("temperature", typeof(TemperatureDevice))]
    public sealed class TemperatureConfigAdapter : DeviceStateAdapter<TemperatureDevice>
    {
        public TemperatureConfigAdapter(TemperatureDevice device)
            : base(device)
        {
        }

        /// <summary>回灌温度配置；缺 tag 时读取为 0。</summary>
        public override void Load(ITagStore tags)
        {
            TypedDevice.Temperature = tags.ReadDouble(FlowTagNaming.ContainerTemperature);
        }

        /// <summary>温度是 items.json 的静态配置，运行期不回写。</summary>
        public override bool Save(ITagStore tags)
        {
            return false;
        }
    }
}
