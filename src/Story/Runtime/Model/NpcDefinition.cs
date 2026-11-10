namespace FluxForge.Story
{
    /// <summary>一个可被到访规则和剧情事件共用的 NPC 身份定义。</summary>
    internal sealed class NpcDefinition
    {
        internal string Identifier { get; }
        internal string DisplayName { get; }
        internal string SpriteKey { get; }
        internal string VisitCountKey { get; }
        internal bool QueueOnGameLoad { get; }

        internal NpcDefinition(
            string identifier,
            string displayName,
            string spriteKey,
            string visitCountKey,
            bool queueOnGameLoad)
        {
            Identifier = identifier;
            DisplayName = displayName;
            SpriteKey = spriteKey;
            VisitCountKey = visitCountKey;
            QueueOnGameLoad = queueOnGameLoad;
        }
    }
}
