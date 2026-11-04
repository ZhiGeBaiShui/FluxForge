namespace FluxForge.Mod
{
    /// <summary>冰块装置适配器：水量用 int tag，温度用 double tag。</summary>
    [DeviceAdapter("ice_block", typeof(IceBlockDevice))]
    public sealed class IceBlockStateAdapter : DeviceStateAdapter<IceBlockDevice>
    {
        public IceBlockStateAdapter(IceBlockDevice device)
            : base(device)
        {
        }

        /// <summary>回灌水量与温度；缺 tag 时读取为 0。</summary>
        public override void Load(ITagStore tags)
        {
            TypedDevice.Water = tags.ReadInt(FlowTagNaming.Water);
            TypedDevice.Temperature = tags.ReadDouble(FlowTagNaming.ContainerTemperature);
        }

        /// <summary>写回全部状态，返回是否有值实际变化。</summary>
        public override bool Save(ITagStore tags)
        {
            bool changed = tags.WriteInt(FlowTagNaming.Water, (int)TypedDevice.Water);
            changed |= tags.WriteDouble(FlowTagNaming.ContainerTemperature, TypedDevice.Temperature);
            return changed;
        }
    }
}
