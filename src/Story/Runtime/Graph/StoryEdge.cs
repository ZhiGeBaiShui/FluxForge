using System;
using System.Threading.Tasks;

namespace FluxForge.Story
{
    /// <summary>
    /// 剧情边：选项文字 + 执行逻辑。
    /// 起点和终点由装配层 Connect 设置；需要条件跳转时用 Branch。
    /// </summary>
    internal abstract class StoryEdge
    {
        internal string Label { get; }
        internal string IconItemKey { get; }
        internal StoryNode From { get; private set; }
        internal StoryNode To { get; private set; }

        private Func<bool> _branchCondition;
        private StoryNode _whenTrue;
        private StoryNode _whenFalse;

        protected StoryEdge(string label, string iconItemKey)
        {
            Label = label;
            IconItemKey = iconItemKey;
        }

        internal abstract Task Execute(StorySequence sequence);

        internal virtual StoryNode ResolveNext()
        {
            if (_branchCondition == null)
            {
                return To;
            }

            return _branchCondition() ? _whenTrue : _whenFalse;
        }

        internal void Branch(
            Func<bool> condition,
            StoryNode whenTrue,
            StoryNode whenFalse)
        {
            _branchCondition = condition;
            _whenTrue = whenTrue;
            _whenFalse = whenFalse;
        }

        internal void Connect(StoryNode from, StoryNode to)
        {
            From = from;
            To = to;
            from?.AddOutgoing(this);
        }
    }
}
