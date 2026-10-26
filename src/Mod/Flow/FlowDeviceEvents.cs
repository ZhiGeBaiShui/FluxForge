using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 装置播报的转发端：把逻辑层的两种播报翻译成物品的外部变化。
    ///
    ///   · <c>Changed</c>   → 把装置状态写进物品 tag（<see cref="FlowTagMap"/>）
    ///   · <c>Exhausted</c> → 把它从当前容器里摘掉
    ///
    /// 职责边界：这里只做「查物品 → 执行副作用」，不做决策、不掩盖失败。
    /// 两个处理器都不吞异常——异常顺着 <c>FlowManager.Tick</c> 上浮到
    /// <c>FlowDriver.Update</c> 的网格级边界，由那里退役整个运行环境。
    /// 容器拒绝移除（<c>Expel</c> 返回 false）属于游戏自己的状态：不重试、不报告。
    /// </summary>
    public static class FlowDeviceEvents
    {
        /// <summary>存量变化：把最新数值写进物品 tag，tooltip 下一次重建时读到新值。</summary>
        public static void OnChanged(IFlowNotifier sender)
        {
            Il2Cpp.GameItem item;
            if (!DeviceRegistry.TryGetItem(sender, out item))
            {
                return;
            }

            FlowTagMap.Write(item.state, sender);
        }

        /// <summary>
        /// 装置耗尽：把它从当前所在的容器里摘掉。
        ///
        /// 用 <c>item.parentInventory</c> 而不是登记时的网格：装置可能被玩家拖到别的容器，
        /// 用物品当前所在的那个容器去 Expel，虚分派会交给实际容器类型处理。
        /// </summary>
        public static void OnExhausted(IFlowNotifier sender)
        {
            Il2Cpp.GameItem item;
            if (!DeviceRegistry.TryGetItem(sender, out item))
            {
                return;
            }

            // Expel 成功时物品可能已经被销毁，之后再读 item.Pointer 不再安全；
            // 所以「先注销再移除」，顺序不能反。
            IntPtr pointer = item.Pointer;
            DeviceRegistry.Unregister(pointer);

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
