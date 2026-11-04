using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// 状态适配器的装置 key。
    ///
    /// key 是 items.json / 持久化 tag 的装置身份，属于 Mod 层；
    /// Logic 装置类不需要知道自己的 key。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public sealed class DeviceAdapterAttribute : Attribute
    {
        public DeviceAdapterAttribute(string key, Type deviceType)
        {
            Key = key;
            DeviceType = deviceType;
        }

        public string Key { get; }

        /// <summary>key 对应的具体装置类型。</summary>
        public Type DeviceType { get; }
    }
}
