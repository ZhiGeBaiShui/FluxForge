namespace FluxForge.Mod
{
    /// <summary>收集装置适配器：按属性注册表读写全部液体与标量存量。</summary>
    public sealed class CollectorStateAdapter : IDeviceStateAdapter
    {
        private readonly CollectorDevice _device;

        public CollectorStateAdapter(CollectorDevice device)
        {
            _device = device;
        }

        public object Device
        {
            get { return _device; }
        }

        /// <summary>回灌全部存量槽位；缺 tag 时读取为 0。</summary>
        public void Load(ITagStore tags)
        {
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                string tag = FlowTagNaming.ForProperty(def.Id, def.Kind);
                double value = def.Kind == FlowPropertyKind.Liquid
                    ? tags.ReadInt(tag)
                    : tags.ReadDouble(tag);
                _device.Stock.Set(def.Key, value);
            }
        }

        /// <summary>写回全部属性，返回是否有值实际变化。</summary>
        public bool Save(ITagStore tags)
        {
            bool changed = false;
            foreach (FlowPropertyDef def in FlowPropertyRegistry.All)
            {
                string tag = FlowTagNaming.ForProperty(def.Id, def.Kind);
                if (def.Kind == FlowPropertyKind.Liquid)
                {
                    changed |= tags.WriteInt(tag, (int)_device.Stock.Get(def.Key));
                }
                else
                {
                    changed |= tags.WriteDouble(tag, _device.Stock.Get(def.Key));
                }
            }

            return changed;
        }
    }
}
