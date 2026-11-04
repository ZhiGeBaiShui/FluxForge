namespace FluxForge.Mod
{
    /// <summary>
    /// 装置适配器使用的最小 tag 读写接口。
    ///
    /// 只负责单个 tag 的存在性、读取和“值变化才写入”；
    /// 不负责 GameItem 生命周期、刷新或序列化策略。
    /// </summary>
    public interface ITagStore
    {
        bool Has(string tag);

        int ReadInt(string tag);

        double ReadDouble(string tag);

        /// <summary>写入整数；值未变化时返回 false。</summary>
        bool WriteInt(string tag, int value);

        /// <summary>写入 double；值未变化时返回 false。</summary>
        bool WriteDouble(string tag, double value);
    }

    /// <summary>
    /// 一台装置对应的 Mod 适配器。
    ///
    /// Device 是 Logic 层的装置实例；适配器知道它属于哪个 GameItem，
    /// 但 Logic 层不知道 tag 名或 GameItem 的存在。
    /// </summary>
    public interface IDeviceStateAdapter
    {
        object Device { get; }

        /// <summary>tag -> 装置。只读，不触发刷新。</summary>
        void Load(ITagStore tags);

        /// <summary>装置 -> tag。返回是否真的写入了值，不触发刷新。</summary>
        bool Save(ITagStore tags);
    }
}
