namespace FluxForge.Story
{
    internal enum StorySpeaker
    {
        Npc,
        Player,
        System
    }

    internal sealed class StoryLineStep : StoryStep
    {
        internal StorySpeaker Speaker { get; }
        internal string Text { get; }
        internal float Pause { get; }

        internal StoryLineStep(StorySpeaker speaker, string text, float pause)
        {
            Speaker = speaker;
            Text = text;
            Pause = pause;
        }
    }
}
