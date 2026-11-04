using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置适配器的脏标记与回写调度。
    ///
    /// Logic 装置播报 <see cref="IFlowNotifier.Changed"/> 时只标脏，不立刻写 tag；
    /// <see cref="FlowDriver.Update"/> 末尾统一 <see cref="Flush"/>，把一帧内
    /// 所有变化聚合后各写一次。干净时不遍历任何装置，因此稳态没有 map 开销。
    ///
    /// <see cref="IFlowNotifier.Exhausted"/> 是例外：物品马上会被移出载体，必须在
    /// 注销 / Expel 之前同步回写，否则最后一份状态丢失。
    /// </summary>
    public static class DeviceStateSync
    {
        /// <summary>本帧有变化、等待回写的 Host；按引用去重。</summary>
        private static readonly List<DeviceHost> _dirty = new List<DeviceHost>();

        /// <summary>tag → 装置。只读回灌，不产生任何写入。</summary>
        public static void Load(DeviceHost host)
        {
            GameItemTagStore tags = new GameItemTagStore(host.Item);
            IReadOnlyList<DeviceStateAdapter> adapters = host.Adapters;

            for (int i = 0; i < adapters.Count; i++)
            {
                adapters[i].Load(tags);
            }
        }

        /// <summary>装置存量变化：本帧结束时统一回写。</summary>
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

        /// <summary>
        /// 装置耗尽：先同步回写最终状态，再交给注册表注销并从载体移除。
        /// 顺序不可调换——Expel 之后物品已经不在网格里。
        /// </summary>
        public static void Exhausted(DeviceHost host)
        {
            Save(host);

            GameItem item = host.Item;
            DeviceRegistry.Unregister(item);
            item.parentInventory.Expel(item);
        }

        /// <summary>注销时摘掉脏标记，避免已注销的 Host 在帧末再次落盘。</summary>
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

        /// <summary>装置 → tag；只要有任一适配器真的写了值，就交给 FlowStock 统一提交。</summary>
        private static void Save(DeviceHost host)
        {
            GameItemTagStore tags = new GameItemTagStore(host.Item);
            IReadOnlyList<DeviceStateAdapter> adapters = host.Adapters;
            bool changed = false;

            for (int i = 0; i < adapters.Count; i++)
            {
                changed |= adapters[i].Save(tags);
            }

            if (!changed)
            {
                return;
            }

            ItemTagCommit.Commit(host.Item);
        }
    }
}
