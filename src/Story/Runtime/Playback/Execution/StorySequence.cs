using System;
using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>边注册的有序步骤；子图调用和插入事件筛选在播放器走到该步时处理。</summary>
    internal sealed class StorySequence
    {
        private readonly List<StoryStep> _steps = new List<StoryStep>();
        internal IReadOnlyList<StoryStep> Steps { get { return _steps; } }

        internal void SayNpc(string content, float pause = 1f)
        {
            _steps.Add(new StoryLineStep(StorySpeaker.Npc, content, pause));
        }

        internal void SayPlayer(string content)
        {
            _steps.Add(new StoryLineStep(StorySpeaker.Player, content, 1f));
        }

        internal void SaySystem(string content)
        {
            _steps.Add(new StoryLineStep(StorySpeaker.System, content, 1f));
        }

        internal void Do(Action action)
        {
            _steps.Add(new StoryActionStep(action));
        }

        internal void Call(StoryAssembly content)
        {
            _steps.Add(new StoryCallStep(content));
        }

        internal void Call(StoryEventDefinition definition, StoryContext context)
        {
            _steps.Add(new StoryCallStep(definition, context));
        }

        internal void Insert(string point)
        {
            _steps.Add(new StoryInsertionStep(point));
        }
    }
}
