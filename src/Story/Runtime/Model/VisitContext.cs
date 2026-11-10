namespace FluxForge.Story
{
    /// <summary>到访规则判断时可见的上下文。</summary>
    internal sealed class VisitContext
    {
        internal NpcDefinition Definition { get; }
        internal IStoryState State { get; }
        internal int Day { get; }

        internal VisitContext(
            NpcDefinition definition,
            IStoryState state,
            int day)
        {
            Definition = definition;
            State = state;
            Day = day;
        }
    }
}
