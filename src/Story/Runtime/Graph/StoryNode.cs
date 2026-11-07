using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>暂停态节点：只保存玩家选项。</summary>
    internal sealed class StoryNode
    {
        private readonly List<StoryEdge> _outgoing = new List<StoryEdge>();

        internal IReadOnlyList<StoryEdge> Outgoing
        {
            get { return _outgoing; }
        }

        internal void Connect(StoryEdge edge, StoryNode to)
        {
            edge.Connect(this, to);
        }

        internal void AddOutgoing(StoryEdge edge)
        {
            _outgoing.Add(edge);
        }
    }
}
