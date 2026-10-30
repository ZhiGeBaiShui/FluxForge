using System.Collections.Generic;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 物品引用 ↔ 装置实例的注册表，外加一张「装置实例 → 物品」的反向表。
    ///
    /// GameItem 没有可以挂字段的地方，所以「这件物品对应哪个装置」只能靠外部字典记住。
    /// 反向表服务于装置的自播报：装置只知道自己，要写 tag / 从网格移除，
    /// 得先找到它挂在哪件物品上。
    ///
    /// Mono 下没有原生指针，改用对象引用做 key。
    /// 读档后游戏会按目录重建物品，旧引用不能继续生效，所以读档时必须整体 Clear()。
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, object> _devices
            = new Dictionary<GameItem, object>();

        /// <summary>装置实例 → 它挂在哪件物品上（事件播报时反查用）。</summary>
        private static readonly Dictionary<object, GameItem> _items
            = new Dictionary<object, GameItem>();

        public static void Register(GameItem item, object device)
        {
            // 同一件物品被重复登记时，先摘掉旧装置的反向条目，
            // 否则那张表会一直留着旧装置，旧装置也永远回收不了
            object previous;
            if (_devices.TryGetValue(item, out previous))
            {
                _items.Remove(previous);
            }

            _devices[item] = device;
            _items[device] = item;
        }

        public static void Unregister(GameItem item)
        {
            if (item == null)
            {
                return;
            }

            // 反向表以装置实例为 key，必须先由物品取出装置才能一并删掉
            object device;
            if (_devices.TryGetValue(item, out device))
            {
                _items.Remove(device);
            }

            _devices.Remove(item);
        }

        /// <summary>
        /// 注销一台机器网格里出现过的全部装置。
        ///
        /// 入参是 <see cref="ProbablyBagMod.Mod.Flow.Adapters.FlowGridAdapter"/> 在启动扫描时记下的物品；
        /// 非装置物品也会传进来，<see cref="Unregister"/> 查不到时自然无操作。
        /// </summary>
        public static void UnregisterAll(IReadOnlyCollection<GameItem> items)
        {
            foreach (GameItem item in items)
            {
                Unregister(item);
            }
        }

        /// <summary>读档 / 换场景时整体失效（旧物品引用不能留着）。</summary>
        public static void Clear()
        {
            _devices.Clear();
            _items.Clear();
        }

        /// <summary>
        /// 装置挂在哪件物品上；查不到返回 null。
        ///
        /// 调用方按「装置与物品生命周期一致」的前提使用它，前提被打破时由调用链上层暴露，
        /// 而不是在这里静默吞掉。
        /// </summary>
        public static GameItem GetItem(object device)
        {
            if (device == null)
            {
                return null;
            }

            GameItem item;
            return _items.TryGetValue(device, out item) ? item : null;
        }

        /// <summary>取实现了 IFlowDevice 的装置（反应类），没有返回 null。</summary>
        public static IFlowDevice GetReactor(GameItem item)
        {
            object device;
            if (item == null || !_devices.TryGetValue(item, out device))
            {
                return null;
            }

            return device as IFlowDevice;
        }

        /// <summary>取实现了 IFlowRouter 的装置（路由类），没有返回 null。</summary>
        public static IFlowRouter GetRouter(GameItem item)
        {
            object device;
            if (item == null || !_devices.TryGetValue(item, out device))
            {
                return null;
            }

            return device as IFlowRouter;
        }

        /// <summary>按具体类型取（调用方自己知道该物品挂的是什么装置）。</summary>
        public static T Get<T>(GameItem item) where T : class
        {
            object device;
            if (item == null || !_devices.TryGetValue(item, out device))
            {
                return null;
            }

            return device as T;
        }
    }
}
