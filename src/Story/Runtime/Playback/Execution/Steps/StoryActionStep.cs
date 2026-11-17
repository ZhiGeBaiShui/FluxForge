using System;

namespace FluxForge.Story
{
    internal sealed class StoryActionStep : StoryStep
    {
        internal Action Action { get; }
        internal StoryActionStep(Action action)
        {
            Action = action;
        }
    }
}
