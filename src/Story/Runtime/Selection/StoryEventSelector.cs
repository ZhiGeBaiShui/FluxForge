using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>
    /// 同一 NPC、同一 Trigger 下只选一个剧情事件。
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
                if (candidate.NpcId != context.Definition.Identifier
                    || candidate.Trigger != context.Trigger)
                {
                    continue;
                }

                if (!candidate.Condition(context))
                {
                    continue;
                }

                if (candidate.MaxTimes > 0
                    && context.State.SeenCount(candidate.Id)
                    >= candidate.MaxTimes)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(candidate.ExclusionGroup)
                    && consumedGroups.Contains(candidate.ExclusionGroup))
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
    }
}
