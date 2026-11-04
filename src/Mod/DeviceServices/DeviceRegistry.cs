using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// GameItem → DeviceHost 的唯一注册表。
    ///
    /// 一件物品只能有一个 Host；Host 内部可以同时持有 Router、Reactor、
    /// Notifier。FlowAudio / FlowAnimation 事件用 Source 反查这里拿 GameItem。
    ///
    /// 生命周期：
    ///   ItemDeviceBinding 构建 Host
    ///   Register  订阅全部 Notifier
    ///   Unregister 退订并移除全部反查项
    ///   Clear     整体失效
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, DeviceHost> _hosts
            = new Dictionary<GameItem, DeviceHost>();

        private static readonly Dictionary<object, GameItem> _deviceItems
            = new Dictionary<object, GameItem>();

        private static readonly Dictionary<IFlowNotifier, GameItem> _items
            = new Dictionary<IFlowNotifier, GameItem>();

        public static void Register(GameItem item, List<object> devices)
        {
            DeviceHost host = new DeviceHost(item, devices);
            _hosts.Add(item, host);

            for (int i = 0; i < devices.Count; i++)
            {
                _deviceItems[devices[i]] = item;
            }

            for (int i = 0; i < host.Notifiers.Count; i++)
            {
                IFlowNotifier notifier = host.Notifiers[i];
                _items.Add(notifier, item);
                FlowDeviceEvents.Subscribe(notifier);
            }
        }

        public static bool Contains(GameItem item)
        {
            return _hosts.ContainsKey(item);
        }

        public static void Unregister(GameItem item)
        {
            DeviceHost host = _hosts[item];

            for (int i = 0; i < host.Components.Count; i++)
            {
                _deviceItems.Remove(host.Components[i]);
            }

            for (int i = 0; i < host.Notifiers.Count; i++)
            {
                IFlowNotifier notifier = host.Notifiers[i];
                _items.Remove(notifier);
                FlowDeviceEvents.Unsubscribe(notifier);
            }

            _hosts.Remove(item);
        }

        public static void Clear()
        {
            foreach (DeviceHost host in _hosts.Values)
            {
                for (int i = 0; i < host.Notifiers.Count; i++)
                {
                    FlowDeviceEvents.Unsubscribe(host.Notifiers[i]);
                }
            }

            _hosts.Clear();
            _deviceItems.Clear();
            _items.Clear();
        }

        /// <summary>装置挂在哪件物品上。</summary>
        public static GameItem GetItem(IFlowNotifier device)
        {
            return _items[device];
        }

        /// <summary>任意装置实例反查所属物品；FlowAudio / FlowAnimation 使用。</summary>
        public static GameItem GetItemForDevice(object device)
        {
            return _deviceItems[device];
        }

        /// <summary>取该物品的路由装置；普通物品没有 Host，返回 null。</summary>
        public static IFlowRouter GetRouter(GameItem item)
        {
            DeviceHost host;
            return _hosts.TryGetValue(item, out host) ? host.Router : null;
        }

        /// <summary>取该物品的反应装置；普通物品没有 Host，返回 null。</summary>
        public static IFlowDevice GetReactor(GameItem item)
        {
            DeviceHost host;
            return _hosts.TryGetValue(item, out host) ? host.Reactor : null;
        }

        /// <summary>在 Host 的全部角色里按具体类型查找。</summary>
        public static T Get<T>(GameItem item) where T : class
        {
            DeviceHost host;
            return _hosts.TryGetValue(item, out host) ? host.Find<T>() : null;
        }
    }
}
