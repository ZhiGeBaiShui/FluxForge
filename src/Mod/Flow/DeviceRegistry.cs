using System;
using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品指针 ↔ 装置实例的注册表，外加一张「装置实例 → 物品」的反向表。
    ///
    /// GameItem 不是 UnityEngine.Object，没有 GetInstanceID()，也没有可以挂字段的地方，
    /// 所以「这件物品对应哪个装置」只能靠外部字典记住。
    ///
    /// 反向表服务于装置的自播报：装置只知道自己，要写 tag / 从网格移除，
    /// 得先找到它挂在哪件物品上。
    ///
    /// 用 Pointer（IntPtr）做 key 而不是 C# 引用：读档后游戏会按目录重建物品，
    /// 旧的 wrapper 失效、原生对象可能被复用，所以读档时必须整体 Clear()。
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<IntPtr, object> _devices
            = new Dictionary<IntPtr, object>();

        /// <summary>装置实例 → 它挂在哪件物品上（事件播报时反查用）。</summary>
        private static readonly Dictionary<object, Il2Cpp.GameItem> _items
            = new Dictionary<object, Il2Cpp.GameItem>();

        public static void Register(IntPtr itemPointer, object device, Il2Cpp.GameItem item)
        {
            // 指针可能被复用到新物品上：先摘掉旧装置的反向条目，
            // 否则那张表会一直留着旧装置，旧装置也永远回收不了
            object previous;
            if (_devices.TryGetValue(itemPointer, out previous))
            {
                _items.Remove(previous);
            }

            _devices[itemPointer] = device;
            _items[device] = item;
        }

        public static void Unregister(IntPtr itemPointer)
        {
            // 反向表以装置实例为 key，必须先由指针取出装置才能一并删掉
            object device;
            if (_devices.TryGetValue(itemPointer, out device))
            {
                _items.Remove(device);
            }

            _devices.Remove(itemPointer);
        }

        /// <summary>
        /// 注销一台机器网格里出现过的全部装置。
        ///
        /// 入参是 <see cref="ProbablyBagMod.Mod.Flow.Adapters.FlowGridAdapter"/> 在启动扫描时记下的物品指针；
        /// 退役路径只遍历这些 IntPtr，不再解引用可能已经失效的原生对象。
        /// 非装置物品的指针也会传进来，<see cref="Unregister"/> 查不到时自然无操作。
        /// </summary>
        public static void UnregisterAll(IReadOnlyCollection<IntPtr> itemPointers)
        {
            foreach (IntPtr pointer in itemPointers)
            {
                Unregister(pointer);
            }
        }

        /// <summary>读档 / 换场景时整体失效（旧指针可能被复用，不能留着）。</summary>
        public static void Clear()
        {
            _devices.Clear();
            _items.Clear();
        }

        /// <summary>装置挂在哪件物品上。查不到返回 false，item 为 null。</summary>
        public static bool TryGetItem(object device, out Il2Cpp.GameItem item)
        {
            if (device == null)
            {
                item = null;
                return false;
            }

            return _items.TryGetValue(device, out item);
        }

        /// <summary>取实现了 IFlowDevice 的装置（反应类），没有返回 null。</summary>
        public static IFlowDevice GetReactor(IntPtr itemPointer)
        {
            object device;
            if (!_devices.TryGetValue(itemPointer, out device))
            {
                return null;
            }

            return device as IFlowDevice;
        }

        /// <summary>取实现了 IFlowRouter 的装置（路由类），没有返回 null。</summary>
        public static IFlowRouter GetRouter(IntPtr itemPointer)
        {
            object device;
            if (!_devices.TryGetValue(itemPointer, out device))
            {
                return null;
            }

            return device as IFlowRouter;
        }

        /// <summary>按具体类型取（调用方自己知道该物品挂的是什么装置）。</summary>
        public static T Get<T>(IntPtr itemPointer) where T : class
        {
            object device;
            if (!_devices.TryGetValue(itemPointer, out device))
            {
                return null;
            }

            return device as T;
        }

        public static int Count
        {
            get { return _devices.Count; }
        }
    }
}
