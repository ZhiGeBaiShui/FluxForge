using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>Host 内的一条装置绑定：key、单例适配器、该物品自己的装置实例。</summary>
    public readonly struct DeviceBinding
    {
        public readonly DeviceStateAdapter Adapter;
        public readonly object Device;

        public DeviceBinding(DeviceStateAdapter adapter, object device)
        {
            Adapter = adapter;
            Device = device;
        }
    }

    /// <summary>
    /// 一件物品对应的全部装置实例与绑定。
    ///
    /// 装置由 <see cref="DeviceStateAdapter.Create"/> 在 <see cref="Create"/> 中创建一次；
    /// 之后的 Load / Save 只更新这些已有实例，绝不重建，保证 Registry / 音频反查
    /// 使用的装置引用始终不变。
    /// </summary>
    public sealed class DeviceHost
    {
        private readonly DeviceBinding[] _bindings;

        private DeviceHost(GameItem item, DeviceBinding[] bindings)
        {
            Item = item;
            _bindings = bindings;

            for (int i = 0; i < bindings.Length; i++)
            {
                object device = bindings[i].Device;

                if (Router == null)
                {
                    Router = device as IFlowRouter;
                }

                if (Reactor == null)
                {
                    Reactor = device as IFlowDevice;
                }
            }
        }

        /// <summary>唯一出生入口：每个定义由适配器创建一台完整初始化的装置。</summary>
        public static DeviceHost Create(GameItem item, List<DeviceDefinition> definitions)
        {
            DeviceBinding[] bindings = new DeviceBinding[definitions.Count];

            for (int i = 0; i < definitions.Count; i++)
            {
                DeviceDefinition definition = definitions[i];
                object device = definition.Adapter.Create(item);
                bindings[i] = new DeviceBinding(definition.Adapter, device);
            }

            return new DeviceHost(item, bindings);
        }

        public GameItem Item { get; }

        public IFlowRouter Router { get; private set; }

        public IFlowDevice Reactor { get; private set; }

        public IReadOnlyList<DeviceBinding> Bindings
        {
            get { return _bindings; }
        }

        /// <summary>tag → 已有装置；不重新创建。</summary>
        public void LoadAll()
        {
            for (int i = 0; i < _bindings.Length; i++)
            {
                DeviceBinding binding = _bindings[i];
                binding.Adapter.Load(Item, binding.Device);
            }
        }

        /// <summary>已有装置 → tag；返回是否有任一适配器实际写入。</summary>
        public bool SaveAll()
        {
            bool changed = false;

            for (int i = 0; i < _bindings.Length; i++)
            {
                DeviceBinding binding = _bindings[i];
                changed |= binding.Adapter.Save(Item, binding.Device);
            }

            return changed;
        }

        /// <summary>装置 Changed 订阅入口：立即写 tag，不排队。</summary>
        public void OnDeviceChanged(IFlowNotifier sender)
        {
            DeviceStateSync.Save(this);
        }

        /// <summary>装置 Exhausted 订阅入口：同步回写后注销并从载体移除。</summary>
        public void OnDeviceExhausted(IFlowNotifier sender)
        {
            DeviceStateSync.Exhausted(this);
        }
    }
}
