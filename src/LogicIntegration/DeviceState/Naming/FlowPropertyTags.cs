using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>属性注册表里的一条 tag 投影：槽位、种类、原版 tag 名。</summary>
    public readonly struct FlowPropertyTag
    {
        public readonly FlowPropertyKey Key;
        public readonly FlowPropertyKind Kind;
        public readonly string Tag;

        public FlowPropertyTag(FlowPropertyKey key, FlowPropertyKind kind, string tag)
        {
            Key = key;
            Kind = kind;
            Tag = tag;
        }
    }

    /// <summary>
    /// 属性 → 原版 tag 名的只读缓存。
    ///
    /// 名字只由 <see cref="FlowPropertyRegistry"/> 和 <see cref="FlowTagNaming"/> 决定，
    /// 启动时生成一次；热路径直接读字符串，不再每次 Load / Save 重新拼。
    /// 缓存属于 Mod 层，Logic 仍然不认识 tag。
    /// </summary>
    public static class FlowPropertyTags
    {
        private static readonly FlowPropertyTag[] _all = Build();

        /// <summary>按属性注册表顺序排列的全部 tag 投影。</summary>
        public static IReadOnlyList<FlowPropertyTag> All
        {
            get { return _all; }
        }

        private static FlowPropertyTag[] Build()
        {
            IReadOnlyList<FlowPropertyDef> defs = FlowPropertyRegistry.All;
            FlowPropertyTag[] tags = new FlowPropertyTag[defs.Count];

            for (int i = 0; i < defs.Count; i++)
            {
                FlowPropertyDef def = defs[i];
                tags[i] = new FlowPropertyTag(
                    def.Key,
                    def.Kind,
                    FlowTagNaming.ForProperty(def.Id, def.Kind));
            }

            return tags;
        }
    }
}
