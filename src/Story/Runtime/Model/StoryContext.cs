namespace FluxForge.Story
{
    /// <summary>剧情事件选择时可见的上下文。</summary>
    internal sealed class StoryContext
    {
        internal NpcDefinition Definition { get; }
        internal IStoryState State { get; }
        internal StoryTrigger Trigger { get; }
        internal VisitKind VisitKind { get; }
        internal int Day { get; }
        internal StoryTradeResult TradeResult { get; }

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
