using System;

namespace FluxForge.Story
{
    /// <summary>
    /// 一段可被选择的剧情事件。Content 不在这里，保持选择与内容执行分离。
    /// </summary>
    internal sealed class StoryEvent
    {
        internal string Id { get; }
        internal string NpcId { get; }
        internal StoryTrigger Trigger { get; }
        internal int Priority { get; }
        internal int MaxTimes { get; }
        internal string ExclusionGroup { get; }
        internal Func<StoryContext, bool> Condition { get; }

        internal StoryEvent(
            string id,
            string npcId,
            StoryTrigger trigger,
            int priority,
            int maxTimes,
            string exclusionGroup,
            Func<StoryContext, bool> condition)
        {
            Id = id;
            NpcId = npcId;
            Trigger = trigger;
            Priority = priority;
            MaxTimes = maxTimes;
            ExclusionGroup = exclusionGroup;
            Condition = condition;
        }
    }
}
