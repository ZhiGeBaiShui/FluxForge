namespace FluxForge.Mod
{
    /// <summary>
    /// 装置适配器使用的最小 tag 读写接口。
    ///
    /// 只负责单个 tag 的读取和“值变化才写入”；
    /// 不负责 GameItem 生命周期、刷新或序列化策略。
    /// </summary>
    public interface ITagStore
    {
        int ReadInt(string tag);

        double ReadDouble(string tag);

        /// <summary>写入整数；值未变化时返回 false。</summary>
        bool WriteInt(string tag, int value);

        /// <summary>写入 double；值未变化时返回 false。</summary>
        bool WriteDouble(string tag, double value);
    }

    /// <summary>
    /// 一台装置对应的 Mod 适配器基类。
    ///
    /// 子类通过 <see cref="DeviceStateAdapter{TDevice}"/> 声明装置类型；
    /// 具体类型由启动期扫描自动登记，新增装置不需要改任何 switch / 注册表。
    /// </summary>
    public abstract class DeviceStateAdapter
    {
        public abstract object Device { get; }

        /// <summary>tag -> 装置。只读，不触发刷新。</summary>
        public abstract void Load(ITagStore tags);

        /// <summary>装置 -> tag。返回是否真的写入了值，不触发刷新。</summary>
        public abstract bool Save(ITagStore tags);
    }

    /// <summary>声明“本适配器服务哪种装置”的泛型基类。</summary>
    public abstract class DeviceStateAdapter<TDevice> : DeviceStateAdapter
        where TDevice : class
    {
        protected DeviceStateAdapter(TDevice device)
        {
            TypedDevice = device;
        }

        protected TDevice TypedDevice { get; }

        public sealed override object Device
        {
            get { return TypedDevice; }
        }
    }
}
