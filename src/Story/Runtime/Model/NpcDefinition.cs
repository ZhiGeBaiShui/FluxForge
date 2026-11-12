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
        internal string FactionId { get; }

        internal NpcDefinition(
            string identifier,
            string displayName,
            string spriteKey,
            string visitCountKey,
            bool queueOnGameLoad,
            string factionId = null)
        {
            Identifier = identifier;
            DisplayName = displayName;
            SpriteKey = spriteKey;
            VisitCountKey = visitCountKey;
            QueueOnGameLoad = queueOnGameLoad;
            FactionId = factionId;
        }
    }
}
