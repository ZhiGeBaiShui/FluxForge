namespace FluxForge.Story
{
    /// <summary>剧情事件的选择数据与执行内容分离。</summary>
    internal sealed class StoryEventDefinition
    {
        internal StoryEvent Event { get; }
        internal StoryAssembly Content { get; }
        internal StoryOutcome Outcome { get; }

        internal StoryEventDefinition(
            StoryEvent storyEvent,
            StoryAssembly content,
            StoryOutcome outcome)
        {
            Event = storyEvent;
            Content = content;
            Outcome = outcome;
        }
    }
}
