namespace ProbablyBagMod.Mod
{
    /// <summary>收集装置：吸收流源并把属性累积起来。</summary>
    public sealed class CollectorAttachment : ItemAttachment
    {
        public override string Name => "collector";

        public override void Attach(Il2Cpp.GameItem item, FlowItemDefinition def)
        {
            CollectorDevice device = new CollectorDevice();
            DeviceRegistry.Register(item.Pointer, device, item);
            FlowDeviceEvents.Subscribe(device);
        }
    }
}
