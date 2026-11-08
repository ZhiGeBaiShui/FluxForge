using System;
using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 一台装置对应的无状态适配器：创建、回灌、回写全部由它执行。
    ///
    /// 适配器本身不持有 <see cref="GameItem"/> / 装置 / Host，
    /// 启动期由 <see cref="DeviceAdapterCatalog"/> 实例化一次后作为单例复用；
    /// 具体类型通过 <see cref="DeviceStateAdapter{TDevice}"/> 声明装置类型，
    /// 新增装置不需要改任何 switch / 注册表。
    /// </summary>
    public abstract class DeviceStateAdapter
    {
        /// <summary>按物品 tag 创建一台完整初始化的装置，不触发回灌。</summary>
        public abstract object Create(GameItem item);

        /// <summary>tag → 已有装置。只更新成员，不重建装置。</summary>
        public abstract void Load(GameItem item, object device);

        /// <summary>装置 → tag。返回是否真的写入了值，不触发提交 / 刷新。</summary>
        public abstract bool Save(GameItem item, object device);
    }

    /// <summary>
    /// 声明“本适配器服务哪种装置”的泛型基类。
    ///
    /// 有状态装置通过 <see cref="FieldMap"/> 声明字段映射，基类按表执行
    /// Load / Save；无状态装置不覆写字段表即可（空表 → 空操作 / false），
    /// 也可以覆写 <see cref="LoadDevice"/> / <see cref="SaveDevice"/> 处理
    /// 无法用单个成员表达的聚合状态（例如收集器的整份存量）。
    /// </summary>
    public abstract class DeviceStateAdapter<TDevice> : DeviceStateAdapter
        where TDevice : class
    {
        /// <summary>空字段表：无状态装置的默认值。</summary>
        private static readonly TagField<TDevice>[] NoFields = Array.Empty<TagField<TDevice>>();

        /// <summary>按物品 tag 创建一台完整初始化的装置。</summary>
        protected abstract TDevice CreateDevice(GameItem item);

        /// <summary>
        /// 成员 ↔ tag 的字段表。静态初始化时由 <see cref="TagFields"/> 编译好
        /// getter / setter，运行期 Load / Save 不再做反射。
        /// </summary>
        protected virtual IReadOnlyList<TagField<TDevice>> FieldMap
        {
            get { return NoFields; }
        }

        /// <summary>按字段表把 tag 回灌到已有装置；不重建装置。</summary>
        protected virtual void LoadDevice(GameItem item, TDevice device)
        {
            IReadOnlyList<TagField<TDevice>> fields = FieldMap;

            for (int i = 0; i < fields.Count; i++)
            {
                fields[i].Load(item, device);
            }
        }

        /// <summary>按字段表把装置写回 tag；返回是否有值实际变化。</summary>
        protected virtual bool SaveDevice(GameItem item, TDevice device)
        {
            bool changed = false;
            IReadOnlyList<TagField<TDevice>> fields = FieldMap;

            for (int i = 0; i < fields.Count; i++)
            {
                changed |= fields[i].Save(item, device);
            }

            return changed;
        }

        /// <summary>泛型装置类型由目录扫描校验，这里只做一次向下转型。</summary>
        public sealed override object Create(GameItem item)
        {
            return CreateDevice(item);
        }

        /// <summary>把 <paramref name="device"/> 转成声明类型后交给 <see cref="LoadDevice"/>。</summary>
        public sealed override void Load(GameItem item, object device)
        {
            LoadDevice(item, (TDevice)device);
        }

        /// <summary>把 <paramref name="device"/> 转成声明类型后交给 <see cref="SaveDevice"/>。</summary>
        public sealed override bool Save(GameItem item, object device)
        {
            return SaveDevice(item, (TDevice)device);
        }
    }
}
