using System;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 装置播报的订阅端：把装置内部状态同步成物品的外部表示。
    ///
    /// 装置（逻辑层）不认识 GameItem / TagSystem，回调只带自己；
    /// 这里用 <see cref="DeviceRegistry"/> 的反向表找回物品，再写 tag 或把它移出载体。
    /// </summary>
    public static class FlowDeviceEvents
    {
        /// <summary>存量变化：把最新数值写进物品 tag，tooltip 下一次重建时读到新值。</summary>
        public static void OnChanged(IFlowNotifier sender)
        {
            // 与 OnExhausted 同理：这是从 Route / TryReact 里同步回调出来的，
            // 异常会顺着 FlowManager.Tick 冒出去，让所有流源当帧停摆。
            // 可达路径：玩家在运行中把收集装置卖掉，登记还在、物品已销毁，
            // 在途流源撞上去仍会走到这里写 tag。
            try
            {
                Il2Cpp.GameItem item;
                if (!DeviceRegistry.TryGetItem(sender, out item))
                {
                    return;
                }

                // 物品已经不在任何容器里（被卖掉 / 被回收）= 这条绑定已经作废。
                // 不主动写 tag：那是解引用一个已经没有归属的对象，只会把错误盖起来。
                // 顺手注销，免得后续每一帧都重复走到这里。
                if (item.parentInventory == null)
                {
                    DeviceRegistry.Unregister(item.Pointer);
                    return;
                }

                FlowTagMap.Write(item.state, sender);
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[流量机] 同步装置状态失败：" + e);
            }
        }

        /// <summary>
        /// 装置耗尽：把它从当前所在的容器里摘掉。
        ///
        /// 用 <c>item.parentInventory</c> 而不是登记时的网格：装置可能被玩家拖到别的容器，
        /// 用物品当前所在的那个容器去 Expel，虚分派会交给实际容器类型处理。
        /// </summary>
        public static void OnExhausted(IFlowNotifier sender)
        {
            // 这是从 TryReact 里同步回调出来的，异常会顺着 FlowManager.Tick 冒出去，
            // 一台装置出问题会让所有流源当帧停摆，必须在这里截断
            try
            {
                Il2Cpp.GameItem item;
                if (!DeviceRegistry.TryGetItem(sender, out item))
                {
                    return;
                }

                // 物品已经被移出网格时父容器会被清空；原生对象已释放则会在这里抛，
                // 由外层 catch 兜住——两种情况都不能再调 Expel
                Il2Cpp.GameInventory inventory = item.parentInventory;
                if (inventory == null)
                {
                    MelonLoader.MelonLogger.Warning(
                        "[流量机] 耗尽装置已不在任何容器中，无法移除：" + item.Pointer);
                    return;
                }

                // Expel 成功时物品可能已经被销毁，之后再读 item.Pointer 不再安全，
                // 所以先记下指针
                IntPtr pointer = item.Pointer;

                if (!inventory.Expel(item))
                {
                    MelonLoader.MelonLogger.Warning(
                        "[流量机] 耗尽装置移除被容器拒绝：" + pointer);
                    return;
                }

                // 物品已经离开载体，指针到装置的映射不再有意义
                DeviceRegistry.Unregister(pointer);
                MelonLoader.MelonLogger.Msg("[流量机] 装置已耗尽，已从载体移除");
            }
            catch (Exception e)
            {
                MelonLoader.MelonLogger.Error("[流量机] 移除耗尽装置失败：" + e);
            }
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
