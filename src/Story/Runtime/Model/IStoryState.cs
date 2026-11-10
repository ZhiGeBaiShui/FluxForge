namespace FluxForge.Story
{
    /// <summary>选择器和剧情条件可读取的 NPC 状态。</summary>
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
