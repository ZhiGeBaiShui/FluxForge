namespace FluxForge.Mod
{
    /// <summary>
    /// 装置播报的转发端：Changed → 写 tag；Exhausted → 注销并从容器移除。
    ///
    /// 订阅 / 退订只由 <see cref="DeviceRegistry"/> 调用。
    /// </summary>
    public static class FlowDeviceEvents
    {
        public static void OnChanged(IFlowNotifier sender)
        {
            FlowStock.Write(DeviceRegistry.GetItem(sender), sender);
        }

        public static void OnExhausted(IFlowNotifier sender)
        {
            GameItem item = DeviceRegistry.GetItem(sender);
            DeviceRegistry.Unregister(item);
            item.parentInventory.Expel(item);
        }

        public static void Subscribe(IFlowNotifier device)
        {
            device.Changed += OnChanged;
            device.Exhausted += OnExhausted;
        }

        public static void Unsubscribe(IFlowNotifier device)
        {
            device.Changed -= OnChanged;
            device.Exhausted -= OnExhausted;
        }
    }
}
