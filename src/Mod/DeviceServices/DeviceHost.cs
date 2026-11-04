using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 一件物品对应的全部装置角色。
    ///
    /// 只负责角色聚合：流程查询从这里取 Router / Reactor / Notifier。
    /// 音效和动画由 Logic 的 FlowAudio / FlowAnimation 事件驱动，
    /// 这里不订阅、不转发具体装置事件。
    /// </summary>
    public sealed class DeviceHost
    {
        private readonly List<object> _components;
        private readonly List<IFlowNotifier> _notifiers;

        public DeviceHost(GameItem item, List<object> components)
        {
            Item = item;
            _components = components;
            _notifiers = new List<IFlowNotifier>();

            for (int i = 0; i < components.Count; i++)
            {
                object component = components[i];

                IFlowRouter router = component as IFlowRouter;
                if (Router == null && router != null)
                {
                    Router = router;
                }

                IFlowDevice reactor = component as IFlowDevice;
                if (Reactor == null && reactor != null)
                {
                    Reactor = reactor;
                }

                IFlowNotifier notifier = component as IFlowNotifier;
                if (notifier != null)
                {
                    _notifiers.Add(notifier);
                }
            }
        }

        public GameItem Item { get; }

        public IFlowRouter Router { get; private set; }

        public IFlowDevice Reactor { get; private set; }

        public IReadOnlyList<object> Components
        {
            get { return _components; }
        }

        public IReadOnlyList<IFlowNotifier> Notifiers
        {
            get { return _notifiers; }
        }

        public T Find<T>() where T : class
        {
            for (int i = 0; i < _components.Count; i++)
            {
                T matched = _components[i] as T;
                if (matched != null)
                {
                    return matched;
                }
            }

            return null;
        }
    }
}
