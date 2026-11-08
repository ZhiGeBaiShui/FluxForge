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
        public DeviceAdapterAttribute(string key)
        {
            Key = key;
        }

        public string Key { get; }
    }
}
