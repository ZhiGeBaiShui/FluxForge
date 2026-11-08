namespace FluxForge.Mod
{
    /// <summary>
    /// 离心机适配器：k 和容量是静态配置，抽出的液体存量需要回灌 / 回写。
    /// 和冷萃装置一样，存量无法用单成员字段表表达，按属性注册表逐槽处理。
    /// </summary>
    [DeviceAdapter("centrifuge")]
    public sealed class CentrifugeStateAdapter : DeviceStateAdapter<CentrifugeDevice>
    {
        protected override CentrifugeDevice CreateDevice(GameItem item)
        {
            return new CentrifugeDevice(
                GameItemTags.ReadDouble(item, FlowTags.CentrifugeK),
                GameItemTags.ReadInt(item, FlowTags.LiquidContainerCapacity));
        }

        protected override void LoadDevice(GameItem item, CentrifugeDevice device)
        {
            device.ReplaceExtracted(ReadStock(item));
        }

        protected override bool SaveDevice(GameItem item, CentrifugeDevice device)
        {
            bool changed = false;
            FlowProperties stock = device.Extracted;

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = stock.Get(property.Key);

                changed |= property.Kind == FlowPropertyKind.Liquid
                    ? GameItemTags.WriteInt(item, property.Tag, (int)value)
                    : GameItemTags.WriteDouble(item, property.Tag, value);
            }

            return changed;
        }

        private static FlowProperties ReadStock(GameItem item)
        {
            FlowProperties stock = new FlowProperties();

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = property.Kind == FlowPropertyKind.Liquid
                    ? GameItemTags.ReadInt(item, property.Tag)
                    : GameItemTags.ReadDouble(item, property.Tag);

                stock.Set(property.Key, value);
            }

            return stock;
        }
    }
}
