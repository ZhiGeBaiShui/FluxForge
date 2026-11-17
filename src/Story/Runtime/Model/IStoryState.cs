namespace FluxForge.Story
{
    /// <summary>NPC 持久状态读写契约；选择器和剧情条件只使用读取方法。</summary>
    internal interface IStoryState
    {
        int VisitCount { get; }
        int LastVisitDay { get; }

        bool HasFlag(string key);
        void MarkFlag(string key);

        bool HasTag(string tag);
        void SetTag(string tag, bool enabled);

        int SeenCount(string eventId);
        void MarkSeen(string eventId);
    }
}
