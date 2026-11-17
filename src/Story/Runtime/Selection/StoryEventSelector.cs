using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>
    /// Select 每次选一个最高优先级事件；插入游标复用 IsEligible 逐个处理候选。
    /// 条件、次数、互斥组和优先级都在这里决定；内容不参与选择。
    /// </summary>
    internal static class StoryEventSelector
    {
        internal static StoryEvent Select(
            IReadOnlyList<StoryEvent> events,
            StoryContext context,
            ICollection<string> consumedGroups)
        {
            StoryEvent selected = null;

            for (int i = 0; i < events.Count; i++)
            {
                StoryEvent candidate = events[i];
                if (!IsEligible(candidate, context, consumedGroups))
                {
                    continue;
                }

                if (selected == null
                    || candidate.Priority > selected.Priority)
                {
                    selected = candidate;
                }
            }

            return selected;
        }

        internal static bool IsEligible(
            StoryEvent candidate,
            StoryContext context,
            ICollection<string> consumedGroups)
        {
            return candidate.NpcId == context.Definition.Identifier
                && candidate.Trigger == context.Trigger
                && candidate.Point == context.Point
                && candidate.Condition(context)
                && (candidate.MaxTimes <= 0
                    || context.State.SeenCount(candidate.Id) < candidate.MaxTimes)
                && (string.IsNullOrEmpty(candidate.ExclusionGroup)
                    || !consumedGroups.Contains(candidate.ExclusionGroup));
        }
    }
}
