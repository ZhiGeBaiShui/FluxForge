using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// 装置上的一个成员 ↔ <see cref="GameItem"/> 的一个 tag。
    ///
    /// 适配器静态初始化时传入已编译好的 getter / setter，
    /// 运行期 Load / Save 只做委托调用与 tag 读写。
    /// </summary>
    public abstract class TagField<TDevice>
    {
        protected TagField(string tag)
        {
            Tag = tag;
        }

        /// <summary>本字段映射的 tag 名（启动期确定，运行期只读）。</summary>
        public string Tag { get; }

        /// <summary>tag → 装置成员。</summary>
        public abstract void Load(GameItem item, TDevice device);

        /// <summary>装置成员 → tag；返回是否真的写入了值。</summary>
        public abstract bool Save(GameItem item, TDevice device);
    }

    /// <summary>字段描述符工厂：显式传入成员 getter / setter。</summary>
    public static class TagFields
    {
        /// <summary>int 成员 ↔ int tag。</summary>
        public static TagField<TDevice> Int<TDevice>(
            string tag,
            Func<TDevice, int> getter,
            Action<TDevice, int> setter)
            where TDevice : class
        {
            return new IntTagField<TDevice>(tag, getter, setter);
        }

        /// <summary>
        /// double 成员 ↔ int tag（按旧行为截断写入）。
        ///
        /// 用于成员是 double、但原版 tag 协议是 int 的装置（例如冰块水量）。
        /// </summary>
        public static TagField<TDevice> Int<TDevice>(
            string tag,
            Func<TDevice, double> getter,
            Action<TDevice, double> setter)
            where TDevice : class
        {
            return new DoubleMemberIntTagField<TDevice>(tag, getter, setter);
        }

        /// <summary>double 成员 ↔ double tag。</summary>
        public static TagField<TDevice> Double<TDevice>(
            string tag,
            Func<TDevice, double> getter,
            Action<TDevice, double> setter)
            where TDevice : class
        {
            return new DoubleTagField<TDevice>(tag, getter, setter);
        }
    }

    /// <summary>int 成员 ↔ int tag。</summary>
    internal sealed class IntTagField<TDevice> : TagField<TDevice>
    {
        private readonly Func<TDevice, int> _getter;
        private readonly Action<TDevice, int> _setter;

        public IntTagField(
            string tag,
            Func<TDevice, int> getter,
            Action<TDevice, int> setter)
            : base(tag)
        {
            _getter = getter;
            _setter = setter;
        }

        public override void Load(GameItem item, TDevice device)
        {
            _setter(device, GameItemTags.ReadInt(item, Tag));
        }

        public override bool Save(GameItem item, TDevice device)
        {
            return GameItemTags.WriteInt(item, Tag, _getter(device));
        }
    }

    /// <summary>double 成员 ↔ int tag；写入按 <c>(int)</c> 截断。</summary>
    internal sealed class DoubleMemberIntTagField<TDevice> : TagField<TDevice>
    {
        private readonly Func<TDevice, double> _getter;
        private readonly Action<TDevice, double> _setter;

        public DoubleMemberIntTagField(
            string tag,
            Func<TDevice, double> getter,
            Action<TDevice, double> setter)
            : base(tag)
        {
            _getter = getter;
            _setter = setter;
        }

        public override void Load(GameItem item, TDevice device)
        {
            _setter(device, GameItemTags.ReadInt(item, Tag));
        }

        public override bool Save(GameItem item, TDevice device)
        {
            return GameItemTags.WriteInt(item, Tag, (int)_getter(device));
        }
    }

    /// <summary>double 成员 ↔ double tag。</summary>
    internal sealed class DoubleTagField<TDevice> : TagField<TDevice>
    {
        private readonly Func<TDevice, double> _getter;
        private readonly Action<TDevice, double> _setter;

        public DoubleTagField(
            string tag,
            Func<TDevice, double> getter,
            Action<TDevice, double> setter)
            : base(tag)
        {
            _getter = getter;
            _setter = setter;
        }

        public override void Load(GameItem item, TDevice device)
        {
            _setter(device, GameItemTags.ReadDouble(item, Tag));
        }

        public override bool Save(GameItem item, TDevice device)
        {
            return GameItemTags.WriteDouble(item, Tag, _getter(device));
        }
    }
}
