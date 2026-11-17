namespace FluxForge.Story
{
    /// <summary>当前子图及返回位置；步骤游标在调用前推进，返回不重跑业务边。</summary>
    internal sealed class StoryCallFrame
    {
        internal StoryAssembly Content { get; }
        internal StoryContext Context { get; }
        internal StoryEvent Event { get; }
        private StoryEdge Edge { get; set; }
        internal StorySequence Sequence { get; private set; }
        internal StoryNode Next { get; private set; }
        internal int Position { get; set; }
        internal StoryInsertionCursor Insertion { get; set; }

        internal StoryCallFrame(StoryAssembly content, StoryEvent storyEvent, StoryContext context)
        {
            Content = content;
            Context = context;
            Event = storyEvent;
            Edge = content.Entry;
        }

        internal void Select(StoryEdge edge)
        {
            Edge = edge;
            Sequence = null;
            Position = 0;
        }

        internal void Register()
        {
            Sequence = new StorySequence();
            Edge.Execute(Sequence);
            Next = Edge.ResolveNext();
        }
    }
}
