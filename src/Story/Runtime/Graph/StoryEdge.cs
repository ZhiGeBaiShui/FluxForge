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

        /// <summary>选中这条边时，要求桌面为空。</summary>
        internal virtual bool RequiresEmptyGrid
        {
            get { return false; }
        }

        /// <summary>选中这条边后立即锁桌面，边结束后释放。</summary>
        internal virtual bool LocksGrid
        {
            get { return false; }
        }

        internal virtual string GridBusyMessage
        {
            get { return null; }
        }

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
