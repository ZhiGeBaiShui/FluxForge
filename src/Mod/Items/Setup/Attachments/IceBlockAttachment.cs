namespace ProbablyBagMod.Mod
{
    /// <summary>冰块：存量从物品的 tag 里读出来，之后由装置自己维护。</summary>
    public sealed class IceBlockAttachment : ItemAttachment
    {
        public override string Name => "ice_block";

        public override string ExclusiveSlot => "device";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            double moisture = FlowTags.ReadInt(item.state, FlowTags.Stock, 0);
            double temperature = FlowTags.ReadFloat(item.state, FlowTags.IceTemperature, 0f);

            IceBlockDevice device = new IceBlockDevice(moisture, temperature);
            DeviceRegistry.Register(item.Pointer, device, item);
            FlowDeviceEvents.Subscribe(device);
        }
    }
}
