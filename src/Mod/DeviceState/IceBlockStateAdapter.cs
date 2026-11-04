namespace FluxForge.Mod
{
    /// <summary>冰块装置适配器：水量用 int tag，温度用 double tag。</summary>
    public sealed class IceBlockStateAdapter : IDeviceStateAdapter
    {
        private readonly IceBlockDevice _device;

        public IceBlockStateAdapter(IceBlockDevice device)
        {
            _device = device;
        }

        public object Device
        {
            get { return _device; }
        }

        /// <summary>只回灌存在的 tag，缺失项保留装置当前值。</summary>
        public void Load(ITagStore tags)
        {
            if (tags.Has(FlowTagNaming.Water))
            {
                _device.Water = tags.ReadInt(FlowTagNaming.Water);
            }

            if (tags.Has(FlowTagNaming.ContainerTemperature))
            {
                _device.Temperature = tags.ReadDouble(FlowTagNaming.ContainerTemperature);
            }
        }

        /// <summary>写回全部状态，返回是否有值实际变化。</summary>
        public bool Save(ITagStore tags)
        {
            bool changed = tags.WriteInt(FlowTagNaming.Water, (int)_device.Water);
            changed |= tags.WriteDouble(FlowTagNaming.ContainerTemperature, _device.Temperature);
            return changed;
        }
    }
}
