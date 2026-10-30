namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 装置播报的转发端：把逻辑层的两种播报翻译成物品的外部变化。
    ///
    ///   · <c>Changed</c>   → 把装置状态写进物品 tag（<see cref="FlowTagMap"/>）
    ///   · <c>Exhausted</c> → 把它从当前容器里摘掉
    ///
    /// 前提：装置与物品的生命周期一致——装置只在装配时登记、在耗尽时注销，没有第三种入口，
    /// 且 <c>Changed</c> 永远先于 <c>Exhausted</c>。所以这里不判断"能不能查到物品"：
    /// 查不到意味着前提被打破，留给调用链上层暴露，而不是在这里静默跳过。
    ///
    /// 已知窗口（见 <c>FlowGridAdapter</c> 的 TODO）：物品先被卖掉 / 移除时登记不会清理，
    /// 在途流源仍会调到这里，写到已离场物品的 tag 上。等网格改为按「拿起 / 放下 / 移出」
    /// 增量维护后，这类物品不会残留在格子表里，窗口自然关闭。
    /// </summary>
    public static class FlowDeviceEvents
    {
        /// <summary>存量变化：把最新数值写进物品 tag，tooltip 下一次重建时读到新值。</summary>
        public static void OnChanged(IFlowNotifier sender)
        {
            FlowStock.Write(DeviceRegistry.GetItem(sender), sender);
        }

        /// <summary>
        /// 装置耗尽：把它从当前所在的容器里摘掉。
        ///
        /// 用 <c>item.parentInventory</c> 而不是登记时的网格：装置可能被玩家拖到别的容器，
        /// 用物品当前所在的那个容器去 Expel，虚分派会交给实际容器类型处理。
        /// </summary>
        public static void OnExhausted(IFlowNotifier sender)
        {
            GameItem item = DeviceRegistry.GetItem(sender);

            // 先注销再移除：Expel 成功时物品可能从容器里消失，
            // 注销查的是登记表，不依赖物品当前是否还能被容器解析。
            DeviceRegistry.Unregister(item);

            item.parentInventory?.Expel(item);
        }

        /// <summary>
        /// 挂上装置的播报事件。
        ///
        /// 不需要显式退订：读档时 <see cref="DeviceRegistry.Clear"/> 丢弃全部引用，
        /// 装置对象再也没有持有者，随后被回收，挂在它身上的订阅列表也一起消失。
        /// </summary>
        public static void Subscribe(IFlowNotifier device)
        {
            device.Changed += OnChanged;
            device.Exhausted += OnExhausted;
        }
    }
}
