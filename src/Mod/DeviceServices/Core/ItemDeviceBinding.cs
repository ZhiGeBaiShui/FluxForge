using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品进入容器时的装置绑定：遍历装置目录里的全部类型名，
    /// 命中哪个 tag 就用哪个类型构造装置，再按装置类型选适配器并从 tag 回灌，
    /// 最后把同一物品的全部装置 / 适配器打包成一个 <see cref="DeviceHost"/>。
    ///
    /// 生命周期 #2（<see cref="ItemAttached"/>）与 #6 读档补绑都走这里。
    /// 绑定是幂等的：已登记的 Host 只重灌适配器，不重建装置。
    /// </summary>
    public static class ItemDeviceBinding
    {
        /// <summary>
        /// 物品进入容器（AddParent）或读档补绑时调用。重复调用安全——移动会反复进入容器。
        /// 返回该物品是否携带装置；返回 false 表示这是普通物品。
        /// </summary>
        public static bool Bind(GameItem item)
        {
            DeviceHost bound = DeviceRegistry.GetHost(item);
            if (bound != null)
            {
                DeviceStateSync.Load(bound);
                return true;
            }

            List<object> devices = new List<object>();

            foreach (string kind in DeviceAdapterCatalog.Keys)
            {
                if (!item.IsTag(kind))
                {
                    continue;
                }

                devices.Add(DeviceAdapterCatalog.CreateDevice(kind));
            }

            if (devices.Count == 0)
            {
                return false;
            }

            DeviceHost host = DeviceHost.Create(item, devices);
            DeviceRegistry.Register(host);
            return true;
        }
    }
}
