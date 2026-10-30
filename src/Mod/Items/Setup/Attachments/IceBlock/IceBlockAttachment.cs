namespace ProbablyBagMod.Mod
{
    /// <summary>冰块：存量从物品的 tag 里读出来，之后由装置自己维护。</summary>
    public sealed class IceBlockAttachment : ItemAttachment
    {
        public override string Name => "ice_block";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            IceBlockDevice device = new IceBlockDevice(0d, 0d);
            FlowStock.ReadInto(item, device);   // tag 是存档真相，先灌进装置

            DeviceRegistry.Register(item, device);
            FlowDeviceEvents.Subscribe(device);
        }
    }
}
