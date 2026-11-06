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
        internal StoryNode To { get; private set; }

        /// <summary>选项被选中后，先在同一 Dialogue 里显示的台词。</summary>
        internal virtual string IntroLine
        {
            get { return null; }
        }

        protected StoryEdge(string label, string iconItemKey)
        {
            Label = label;
            IconItemKey = iconItemKey;
        }

        internal abstract Task Execute(StoryPlayer player);

        internal void Connect(StoryNode from, StoryNode to)
        {
            To = to;
            from?.AddOutgoing(this);
        }
    }
}
