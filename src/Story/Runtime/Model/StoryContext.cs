namespace FluxForge.Story
{
    /// <summary>事件选择与播放帧共享的上下文；插入点上下文保留原阶段的交易结果。</summary>
    internal sealed class StoryContext
    {
        internal NpcDefinition Definition { get; }
        internal IStoryState State { get; }
        internal StoryTrigger Trigger { get; }
        internal VisitKind VisitKind { get; }
        internal int Day { get; }
        internal StoryTradeResult TradeResult { get; }
        internal string Point { get; }

        internal StoryContext(
            NpcDefinition definition,
            IStoryState state,
            StoryTrigger trigger,
            VisitKind visitKind,
            int day,
            StoryTradeResult tradeResult)
        {
            Definition = definition;
            State = state;
            Trigger = trigger;
            VisitKind = visitKind;
            Day = day;
            TradeResult = tradeResult;
        }

        internal StoryContext(StoryContext visit, string point)
            : this(visit.Definition, visit.State, StoryTrigger.InsertionPoint,
                visit.VisitKind, visit.Day, visit.TradeResult)
        {
            Point = point;
        }

        internal int VisitCount
        {
            get { return State.VisitCount; }
        }

        internal bool HasFlag(string key)
        {
            return State.HasFlag(key);
        }

        internal bool HasTag(string tag)
        {
            return State.HasTag(tag);
        }
    }
}
