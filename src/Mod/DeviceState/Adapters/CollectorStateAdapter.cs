namespace FluxForge.Mod
{
    /// <summary>收集装置适配器：按属性注册表读写全部液体与标量存量。</summary>
    [DeviceAdapter("collector", typeof(CollectorDevice))]
    public sealed class CollectorStateAdapter : DeviceStateAdapter<CollectorDevice>
    {
        public CollectorStateAdapter(CollectorDevice device)
            : base(device)
        {
        }

        /// <summary>回灌全部存量槽位；缺 tag 时读取为 0。</summary>
        public override void Load(ITagStore tags)
        {
            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = property.Kind == FlowPropertyKind.Liquid
                    ? tags.ReadInt(property.Tag)
                    : tags.ReadDouble(property.Tag);
                TypedDevice.Stock.Set(property.Key, value);
            }
        }

        /// <summary>写回全部属性，返回是否有值实际变化。</summary>
        public override bool Save(ITagStore tags)
        {
            bool changed = false;
            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                if (property.Kind == FlowPropertyKind.Liquid)
                {
                    changed |= tags.WriteInt(property.Tag, (int)TypedDevice.Stock.Get(property.Key));
                }
                else
                {
                    changed |= tags.WriteDouble(property.Tag, TypedDevice.Stock.Get(property.Key));
                }
            }

            return changed;
        }
    }
}
