namespace FluxForge.Story
{
    internal sealed class StoryInsertionStep : StoryStep
    {
        internal string Point { get; }
        internal StoryInsertionStep(string point)
        {
            Point = point;
        }
    }
}
