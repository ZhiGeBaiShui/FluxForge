using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// GameItem → DeviceHost 的唯一注册表。
    ///
    /// Host 内部持有 (Key, Adapter, Device) 绑定；Registry 只负责正反查与事件订阅。
    /// 装置由 Adapter 在 Host.Create 中创建一次，之后 Load/Save 不重建。
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, DeviceHost> _hosts
            = new Dictionary<GameItem, DeviceHost>();

        /// <summary>装置实例 → 物品，FlowAudio / FlowAnimation 反查使用。</summary>
        private static readonly Dictionary<object, GameItem> _deviceItems
            = new Dictionary<object, GameItem>();

        public static void Register(DeviceHost host)
        {
            _hosts.Add(host.Item, host);

            IReadOnlyList<DeviceBinding> bindings = host.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                _deviceItems.Add(bindings[i].Device, host.Item);
            }

            Subscribe(host);
        }

        public static void Unregister(GameItem item)
        {
            DeviceHost host = _hosts[item];
            Unsubscribe(host);

            IReadOnlyList<DeviceBinding> bindings = host.Bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                _deviceItems.Remove(bindings[i].Device);
            }

            _hosts.Remove(item);
        }

        public static void Clear()
        {
            foreach (DeviceHost host in _hosts.Values)
            {
                Unsubscribe(host);
            }

            _hosts.Clear();
            _deviceItems.Clear();
        }

        /// <summary>取该物品的 Host；普通物品没有 Host，返回 null。</summary>
        public static DeviceHost GetHost(GameItem item)
        {
            DeviceHost host;
            return _hosts.TryGetValue(item, out host) ? host : null;
        }

        /// <summary>任意装置实例反查所属物品；FlowAudio / FlowAnimation 使用。</summary>
        public static GameItem GetItemForDevice(object device)
        {
            return _deviceItems[device];
        }

        /// <summary>取该物品的路由装置；普通物品没有 Host，返回 null。</summary>
        public static IFlowRouter GetRouter(GameItem item)
        {
            DeviceHost host = GetHost(item);
            return host == null ? null : host.Router;
        }

        /// <summary>取该物品的反应装置；普通物品没有 Host，返回 null。</summary>
        public static IFlowDevice GetReactor(GameItem item)
        {
            DeviceHost host = GetHost(item);
            return host == null ? null : host.Reactor;
        }

        /// <summary>把物品朝向和镜像位广播给该物品的所有 IFacing 装置。</summary>
        public static void SetFacing(GameItem item, int facing, bool flipped)
        {
            DeviceHost host = GetHost(item);
            if (host != null)
            {
                host.SetFacing(facing, flipped);
            }
        }

        private static void Subscribe(DeviceHost host)
        {
            IReadOnlyList<DeviceBinding> bindings = host.Bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                IFlowNotifier notifier = bindings[i].Device as IFlowNotifier;
                if (notifier != null)
                {
                    notifier.Changed += host.OnDeviceChanged;
                    notifier.Exhausted += host.OnDeviceExhausted;
                }
            }
        }

        private static void Unsubscribe(DeviceHost host)
        {
            IReadOnlyList<DeviceBinding> bindings = host.Bindings;

            for (int i = 0; i < bindings.Count; i++)
            {
                IFlowNotifier notifier = bindings[i].Device as IFlowNotifier;
                if (notifier != null)
                {
                    notifier.Changed -= host.OnDeviceChanged;
                    notifier.Exhausted -= host.OnDeviceExhausted;
                }
            }
        }
    }
}
