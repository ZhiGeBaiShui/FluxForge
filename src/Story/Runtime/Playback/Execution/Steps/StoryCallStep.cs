namespace FluxForge.Story
{
    internal sealed class StoryCallStep : StoryStep
    {
        internal StoryAssembly Content { get; }
        internal StoryContext Context { get; }
        internal StoryEvent Event { get; }
        internal StoryCallStep(StoryAssembly content)
        {
            Content = content;
        }

        internal StoryCallStep(StoryEventDefinition definition, StoryContext context)
        {
            Content = definition.Content;
            Context = context;
            Event = definition.Event;
        }
    }
}
