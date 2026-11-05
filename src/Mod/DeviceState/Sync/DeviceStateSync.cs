using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置状态回写的脏标记与帧末调度。
    ///
    /// 适配器负责 Load / Save 本身；本类只决定何时 Save。
    /// Changed 只标脏，FlowDriver.Update 末尾统一 Flush；Exhausted 先同步 Save 再移除。
    /// </summary>
    public static class DeviceStateSync
    {
        private static readonly List<DeviceHost> _dirty = new List<DeviceHost>();

        public static void MarkDirty(DeviceHost host)
        {
            if (host.IsDirty)
            {
                return;
            }

            host.IsDirty = true;
            _dirty.Add(host);
        }

        /// <summary>帧末回写全部脏 Host；干净时是一次空遍历。</summary>
        public static void Flush()
        {
            for (int i = 0; i < _dirty.Count; i++)
            {
                DeviceHost host = _dirty[i];
                host.IsDirty = false;
                Save(host);
            }

            _dirty.Clear();
        }

        /// <summary>装置耗尽：先同步回写，再注销并移出载体。</summary>
        public static void Exhausted(DeviceHost host)
        {
            Save(host);

            GameItem item = host.Item;
            DeviceRegistry.Unregister(item);
            item.parentInventory.Expel(item);
        }

        /// <summary>注销时摘掉脏标记，避免已注销 Host 在帧末再次落盘。</summary>
        public static void Forget(DeviceHost host)
        {
            if (!host.IsDirty)
            {
                return;
            }

            host.IsDirty = false;
            _dirty.Remove(host);
        }

        /// <summary>整表清空（场景卸载 / 退档 / mod 卸载）。</summary>
        public static void Clear()
        {
            for (int i = 0; i < _dirty.Count; i++)
            {
                _dirty[i].IsDirty = false;
            }

            _dirty.Clear();
        }

        private static void Save(DeviceHost host)
        {
            if (host.SaveAll())
            {
                ItemTagCommit.Commit(host.Item);
            }
        }
    }
}
