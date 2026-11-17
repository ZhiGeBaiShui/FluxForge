using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>播放器共享的 NPC 注册内容、来访上下文和整次来访已消费的互斥组。</summary>
    internal sealed class StoryPlaybackContext
    {
        internal NpcStoryContent Content { get; }
        internal StoryContext Visit { get; }
        internal ICollection<string> ConsumedGroups { get; }

        internal StoryPlaybackContext(
            NpcStoryContent content, StoryContext visit, ICollection<string> consumedGroups)
        {
            Content = content;
            Visit = visit;
            ConsumedGroups = consumedGroups;
        }
    }
}
