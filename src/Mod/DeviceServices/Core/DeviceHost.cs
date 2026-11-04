using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 一件物品对应的全部装置角色与状态适配器。
    ///
    /// 角色聚合：流程查询从这里取 Router / Reactor。
    /// 状态聚合：每个 Logic 装置各有一个 <see cref="DeviceStateAdapter"/>，
    /// 回灌 / 回写都按这个列表逐台执行。
    ///
    /// 事件订阅由 <see cref="DeviceRegistry"/> 完成；Changed 只标脏，
    /// Exhausted 同步回写后注销并从载体移除。
    /// </summary>
    public sealed class DeviceHost
    {
        private readonly List<object> _components;
        private readonly List<DeviceStateAdapter> _adapters;

        private DeviceHost(GameItem item, List<object> components, List<DeviceStateAdapter> adapters)
        {
            Item = item;
            _components = components;
            _adapters = adapters;

            for (int i = 0; i < components.Count; i++)
            {
                object component = components[i];

                if (Router == null)
                {
                    Router = component as IFlowRouter;
                }

                if (Reactor == null)
                {
                    Reactor = component as IFlowDevice;
                }
            }
        }

        /// <summary>
        /// 唯一构建入口：为每个装置创建适配器并从 tag 回灌一次。
        /// 绑定与注册分离，<see cref="DeviceRegistry"/> 不再重复创建适配器。
        /// </summary>
        public static DeviceHost Create(GameItem item, List<object> components)
        {
            GameItemTagStore tags = new GameItemTagStore(item);
            List<DeviceStateAdapter> adapters = new List<DeviceStateAdapter>(components.Count);

            for (int i = 0; i < components.Count; i++)
            {
                DeviceStateAdapter adapter = DeviceAdapterCatalog.CreateAdapter(components[i]);
                adapter.Load(tags);
                adapters.Add(adapter);
            }

            return new DeviceHost(item, components, adapters);
        }

        public GameItem Item { get; }

        public IFlowRouter Router { get; private set; }

        public IFlowDevice Reactor { get; private set; }

        /// <summary>本物品上的全部 Logic 装置实例。</summary>
        public IReadOnlyList<object> Components
        {
            get { return _components; }
        }

        /// <summary>与 <see cref="Components"/> 一一对应的状态适配器。</summary>
        public IReadOnlyList<DeviceStateAdapter> Adapters
        {
            get { return _adapters; }
        }

        /// <summary>是否有尚未回写的装置状态；由 <see cref="DeviceStateSync"/> 维护。</summary>
        public bool IsDirty { get; set; }

        /// <summary>装置 Changed 订阅入口；委托身份按 target + method 比较，退订可用同一方法组。</summary>
        public void OnDeviceChanged(IFlowNotifier sender)
        {
            DeviceStateSync.MarkDirty(this);
        }

        /// <summary>装置 Exhausted 订阅入口：同步回写后注销并移除物品。</summary>
        public void OnDeviceExhausted(IFlowNotifier sender)
        {
            DeviceStateSync.Exhausted(this);
        }
    }
}
