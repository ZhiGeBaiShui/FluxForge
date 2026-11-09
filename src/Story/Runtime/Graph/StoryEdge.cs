using System.Threading.Tasks;

namespace FluxForge.Story
{
    /// <summary>
    /// 剧情边：选项文字 + 执行逻辑。
    /// 起点和终点由装配层 Connect 设置。
    /// </summary>
    internal abstract class StoryEdge
    {
        internal string Label { get; }
        internal string IconItemKey { get; }
        internal StoryNode From { get; private set; }
        internal StoryNode To { get; private set; }

        protected StoryEdge(string label, string iconItemKey)
        {
            Label = label;
            IconItemKey = iconItemKey;
        }

        internal abstract Task Execute(StorySequence sequence);

        internal void Connect(StoryNode from, StoryNode to)
        {
            From = from;
            To = to;
            from?.AddOutgoing(this);
        }
    }
}
