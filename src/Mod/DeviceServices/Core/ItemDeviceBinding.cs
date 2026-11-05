using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品进入容器时的装置绑定：由目录找出物品携带的装置定义，
    /// 交给 <see cref="DeviceHost.Create"/> 让适配器创建装置并保存绑定。
    ///
    /// 生命周期 #2（<see cref="ItemAttached"/>）与 #6 读档补绑都走这里。
    /// 已有 Host 只刷新状态，不重建装置。
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
                bound.LoadAll();
                return true;
            }

            List<DeviceDefinition> definitions = DeviceAdapterCatalog.FindFor(item);
            if (definitions.Count == 0)
            {
                return false;
            }

            DeviceHost host = DeviceHost.Create(item, definitions);
            DeviceRegistry.Register(host);
            return true;
        }
    }
}
