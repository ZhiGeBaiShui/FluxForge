using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>一次到访内剧情选择与执行的临时状态。</summary>
    internal sealed class NpcVisitSession
    {
        internal NpcStoryContent Content { get; }
        internal VisitDecision Decision { get; }
        internal StoryContext Context { get; }
        internal HashSet<string> ConsumedGroups { get; }

        internal NpcVisitSession(
            NpcStoryContent content,
            VisitDecision decision,
            StoryContext context)
        {
            Content = content;
            Decision = decision;
            Context = context;
            ConsumedGroups = new HashSet<string>();
        }
    }
}
