using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品引用 ↔ 装置实例的注册表，外加一张「装置实例 → 物品」的反向表。
    ///
    /// 登记即订阅、注销即退订；订阅点只有这里。
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, IFlowNotifier> _devices
            = new Dictionary<GameItem, IFlowNotifier>();

        private static readonly Dictionary<IFlowNotifier, GameItem> _items
            = new Dictionary<IFlowNotifier, GameItem>();

        public static void Register(GameItem item, IFlowNotifier device)
        {
            _devices[item] = device;
            _items[device] = item;

            FlowDeviceEvents.Subscribe(device);
        }

        /// <summary>
        /// 该物品是否已经登记过装置。
        ///
        /// 落地绑定必须先查再建：同网格移动是「Expel + Accept」，每次都会重新进入网格，
        /// 不查就会每次都白造一台装置再丢弃。
        /// </summary>
        public static bool Contains(GameItem item)
        {
            return _devices.ContainsKey(item);
        }

        public static void Unregister(GameItem item)
        {
            IFlowNotifier device = _devices[item];
            _devices.Remove(item);
            _items.Remove(device);

            FlowDeviceEvents.Unsubscribe(device);
        }

        /// <summary>读档 / 换场景时整体失效。</summary>
        public static void Clear()
        {
            foreach (IFlowNotifier device in _devices.Values)
            {
                FlowDeviceEvents.Unsubscribe(device);
            }

            _devices.Clear();
            _items.Clear();
        }

        /// <summary>装置挂在哪件物品上。</summary>
        public static GameItem GetItem(IFlowNotifier device)
        {
            return _items[device];
        }

        /// <summary>取实现了 IFlowDevice 的装置（反应类）。</summary>
        public static IFlowDevice GetReactor(GameItem item)
        {
            IFlowNotifier device;
            return _devices.TryGetValue(item, out device) ? device as IFlowDevice : null;
        }

        /// <summary>取实现了 IFlowRouter 的装置（路由类）。</summary>
        public static IFlowRouter GetRouter(GameItem item)
        {
            IFlowNotifier device;
            return _devices.TryGetValue(item, out device) ? device as IFlowRouter : null;
        }

        /// <summary>按具体类型取。</summary>
        public static T Get<T>(GameItem item) where T : class
        {
            IFlowNotifier device;
            return _devices.TryGetValue(item, out device) ? device as T : null;
        }
    }
}
