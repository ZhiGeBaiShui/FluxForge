using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品 ↔ 装置实例的注册表，按角色分三张表：
    ///   · 正向两张（路由 / 反应）存具体接口，查询时不用再向下转型；
    ///   · 反向一张（播报者 → 物品）供 <see cref="FlowDeviceEvents"/> 回写 tag。
    ///
    /// 一个物品最多一个路由 + 一个反应，两张正向表各自独立。
    /// 没有状态变化的装置可以不实现 <see cref="IFlowNotifier"/>：
    /// 这时反向表里没有它，事件订阅自然跳过——静默丢失是正确的语义。
    ///
    /// 登记即订阅、注销即退订；订阅点只有这里。
    ///
    /// 登记表是独立的静态根：物品离开网格、网格退役都只摘格表与流源，不会注销装置；
    /// 条目只由装置播报 Exhausted（<see cref="FlowDeviceEvents"/>）或整体清表移除。
    /// </summary>
    public static class DeviceRegistry
    {
        private static readonly Dictionary<GameItem, IFlowRouter> _routers
            = new Dictionary<GameItem, IFlowRouter>();

        private static readonly Dictionary<GameItem, IFlowDevice> _reactors
            = new Dictionary<GameItem, IFlowDevice>();

        private static readonly Dictionary<IFlowNotifier, GameItem> _items
            = new Dictionary<IFlowNotifier, GameItem>();

        /// <summary>
        /// 登记一台装置。按它实现的角色接口落到对应正向表；
        /// 只有实现了 <see cref="IFlowNotifier"/> 的才进反向表并订阅事件，
        /// 没有状态变化的装置到这里就结束，不订阅任何事件。
        /// </summary>
        public static void Register(GameItem item, object device)
        {
            IFlowRouter router = device as IFlowRouter;
            if (router != null)
            {
                _routers[item] = router;
            }

            IFlowDevice reactor = device as IFlowDevice;
            if (reactor != null)
            {
                _reactors[item] = reactor;
            }

            IFlowNotifier notifier = device as IFlowNotifier;
            if (notifier == null)
            {
                return;
            }

            _items[notifier] = item;
            FlowDeviceEvents.Subscribe(notifier);
        }

        /// <summary>
        /// 该物品是否已经登记过装置。
        ///
        /// 落地绑定必须先查再建：同网格移动会重新走 RemoveParent + AddParent，
        /// 每次都会重新进入网格，不查就会每次都白造一台装置再丢弃。
        /// </summary>
        public static bool Contains(GameItem item)
        {
            return _routers.ContainsKey(item) || _reactors.ContainsKey(item);
        }

        public static void Unregister(GameItem item)
        {
            IFlowRouter router;
            if (_routers.TryGetValue(item, out router))
            {
                _routers.Remove(item);
                Detach(router as IFlowNotifier);
            }

            IFlowDevice reactor;
            if (_reactors.TryGetValue(item, out reactor))
            {
                _reactors.Remove(item);
                Detach(reactor as IFlowNotifier);
            }
        }

        /// <summary>
        /// 退出存档 / 场景卸载 / mod 卸载时整体失效
        /// （<see cref="FlowDriver.Reset"/> / <see cref="FlowDriver.Shutdown"/> 调用）。
        /// </summary>
        public static void Clear()
        {
            foreach (IFlowNotifier device in _items.Keys)
            {
                FlowDeviceEvents.Unsubscribe(device);
            }

            _routers.Clear();
            _reactors.Clear();
            _items.Clear();
        }

        /// <summary>装置挂在哪件物品上。</summary>
        public static GameItem GetItem(IFlowNotifier device)
        {
            return _items[device];
        }

        /// <summary>取该物品的路由装置；没有返回 null。</summary>
        public static IFlowRouter GetRouter(GameItem item)
        {
            IFlowRouter router;
            return _routers.TryGetValue(item, out router) ? router : null;
        }

        /// <summary>取该物品的反应装置；没有返回 null。</summary>
        public static IFlowDevice GetReactor(GameItem item)
        {
            IFlowDevice reactor;
            return _reactors.TryGetValue(item, out reactor) ? reactor : null;
        }

        /// <summary>按具体类型取（先查路由表，再查反应表）。</summary>
        public static T Get<T>(GameItem item) where T : class
        {
            IFlowRouter router;
            if (_routers.TryGetValue(item, out router))
            {
                T matched = router as T;
                if (matched != null)
                {
                    return matched;
                }
            }

            IFlowDevice reactor;
            if (_reactors.TryGetValue(item, out reactor))
            {
                return reactor as T;
            }

            return null;
        }

        /// <summary>从反向表摘除并退订；没有实现 IFlowNotifier 的装置走到这里是空操作。</summary>
        private static void Detach(IFlowNotifier device)
        {
            if (device == null)
            {
                return;
            }

            _items.Remove(device);
            FlowDeviceEvents.Unsubscribe(device);
        }
    }
}
