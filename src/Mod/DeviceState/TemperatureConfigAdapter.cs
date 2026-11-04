namespace FluxForge.Mod
{
    /// <summary>温度控制装置适配器：只从 tag 读取初始温度配置。</summary>
    public sealed class TemperatureConfigAdapter : IDeviceStateAdapter
    {
        private readonly TemperatureDevice _device;

        public TemperatureConfigAdapter(TemperatureDevice device)
        {
            _device = device;
        }

        public object Device
        {
            get { return _device; }
        }

        /// <summary>只回灌存在的温度 tag，缺失时保留构造时的配置值。</summary>
        public void Load(ITagStore tags)
        {
            if (tags.Has(FlowTagNaming.ContainerTemperature))
            {
                _device.Temperature = tags.ReadDouble(FlowTagNaming.ContainerTemperature);
            }
        }

        /// <summary>温度是 items.json 的静态配置，运行期不回写。</summary>
        public bool Save(ITagStore tags)
        {
            return false;
        }
    }
}
