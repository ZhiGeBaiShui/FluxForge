using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// GameItem → DeviceHost 的唯一注册表。
    ///
    /// 一件物品只有一个 Host；Host 内部同时持有全部 Logic 装置、
    /// Router / Reactor 角色和一一对应的状态适配器。
    ///
    /// 生命周期：
    ///   DeviceHost.Create 构建装置与适配器（先回灌 tag）
    ///   Register    建立正 / 反查表，订阅 IFlowNotifier.Changed / Exhausted
    ///   Unregister  退订、摘除全部反查项、清掉脏标记
    ///   Clear       整体失效
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, DeviceHost> _hosts
            = new Dictionary<GameItem, DeviceHost>();

        /// <summary>装置实例 → 物品，FlowAudio / FlowAnimation 反查使用。</summary>
        private static readonly Dictionary<object, GameItem> _deviceItems
            = new Dictionary<object, GameItem>();

        /// <summary>Host 已由 <see cref="DeviceHost.Create"/> 构建并完成 tag 回灌。</summary>
        public static void Register(DeviceHost host)
        {
            _hosts.Add(host.Item, host);

            IReadOnlyList<object> components = host.Components;
            for (int i = 0; i < components.Count; i++)
            {
                _deviceItems.Add(components[i], host.Item);
            }

            Subscribe(host);
        }

        public static void Unregister(GameItem item)
        {
            DeviceHost host = _hosts[item];
            Unsubscribe(host);

            IReadOnlyList<object> components = host.Components;
            for (int i = 0; i < components.Count; i++)
            {
                _deviceItems.Remove(components[i]);
            }

            _hosts.Remove(item);
            DeviceStateSync.Forget(host);
        }

        public static void Clear()
        {
            foreach (DeviceHost host in _hosts.Values)
            {
                Unsubscribe(host);
            }

            _hosts.Clear();
            _deviceItems.Clear();
            DeviceStateSync.Clear();
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

        /// <summary>订阅装置播报；Changed 只标脏，Exhausted 同步回写后注销。</summary>
        private static void Subscribe(DeviceHost host)
        {
            IReadOnlyList<DeviceStateAdapter> adapters = host.Adapters;

            for (int i = 0; i < adapters.Count; i++)
            {
                object device = adapters[i].Device;

                IFlowNotifier notifier = device as IFlowNotifier;
                if (notifier != null)
                {
                    notifier.Changed += host.OnDeviceChanged;
                    notifier.Exhausted += host.OnDeviceExhausted;
                }
            }
        }

        /// <summary>退订；与 <see cref="Subscribe"/> 使用同一组方法组委托，按 target + method 相等退订。</summary>
        private static void Unsubscribe(DeviceHost host)
        {
            IReadOnlyList<DeviceStateAdapter> adapters = host.Adapters;

            for (int i = 0; i < adapters.Count; i++)
            {
                object device = adapters[i].Device;

                IFlowNotifier notifier = device as IFlowNotifier;
                if (notifier != null)
                {
                    notifier.Changed -= host.OnDeviceChanged;
                    notifier.Exhausted -= host.OnDeviceExhausted;
                }
            }
        }
    }
}
