using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品进入容器时的装置绑定：遍历装置目录里的全部类型名，
    /// 命中哪个 tag 就用哪个类型构造装置，再用 tag 映射把状态写进装置，
    /// 最后把同一物品的全部角色打包成一个 <see cref="DeviceHost"/>。
    ///
    /// 生命周期 #2（<see cref="ItemAttached"/>）与 #6 读档补绑都走这里。
    /// </summary>
    internal static class ItemDeviceBinding
    {
        /// <summary>
        /// 物品进入容器（AddParent）或读档补绑时调用。重复调用安全——移动会反复进入容器。
        /// 返回该物品是否携带装置；返回 false 表示这是普通物品。
        /// </summary>
        internal static bool Bind(GameItem item)
        {
            if (DeviceRegistry.Contains(item))
            {
                return true;
            }

            List<object> devices = new List<object>();

            foreach (string kind in FlowDeviceCatalog.Keys)
            {
                if (!item.IsTag(kind))
                {
                    continue;
                }

                object device = FlowDeviceCatalog.Create(kind);

                // 有状态槽位的装置才读 tag；没有状态的装置跳过这一步。
                IFlowState state = device as IFlowState;
                if (state != null)
                {
                    FlowStock.ReadInto(item, state);
                }

                devices.Add(device);
            }

            if (devices.Count == 0)
            {
                return false;
            }

            DeviceRegistry.Register(item, devices);
            return true;
        }
    }
}
