using System;

namespace FluxForge.Story
{
    /// <summary>
    /// 剧情边：选项文字 + 执行逻辑。
    /// Connect 设置下一节点并把选项挂到起点；需要条件跳转时用 Branch。
    /// </summary>
    internal abstract class StoryEdge
    {
        private readonly string _labelKey;
        internal string Label
        {
            get { return _labelKey == null ? null : ModHelper.GetLocalized("fluxforge", _labelKey); }
        }
        internal string IconItemKey { get; }
        private StoryNode _to;

        private Func<bool> _branchCondition;
        private StoryNode _whenTrue;
        private StoryNode _whenFalse;

        protected StoryEdge(string label, string iconItemKey)
        {
            _labelKey = label;
            IconItemKey = iconItemKey;
        }

        internal abstract void Execute(StorySequence sequence);

        internal virtual StoryNode ResolveNext()
        {
            if (_branchCondition == null)
            {
                return _to;
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
            _to = to;
            from?.AddOutgoing(this);
        }
    }
}
