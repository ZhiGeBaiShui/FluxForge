using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>一个 NPC 的完整剧情配置：身份、到访规则、剧情事件。</summary>
    internal sealed class NpcStoryContent
    {
        internal NpcDefinition Definition { get; }
        internal VisitRule[] VisitRules { get; }
        internal StoryEventDefinition[] EventDefinitions { get; }
        internal IReadOnlyList<StoryEvent> Events { get; }

        internal NpcStoryContent(
            NpcDefinition definition,
            VisitRule[] visitRules,
            StoryEventDefinition[] eventDefinitions)
        {
            Definition = definition;
            VisitRules = visitRules;
            EventDefinitions = eventDefinitions;

            StoryEvent[] events = new StoryEvent[eventDefinitions.Length];
            for (int i = 0; i < eventDefinitions.Length; i++)
            {
                events[i] = eventDefinitions[i].Event;
            }

            Events = events;
        }

        internal StoryEventDefinition FindEvent(string eventId)
        {
            for (int i = 0; i < EventDefinitions.Length; i++)
            {
                if (EventDefinitions[i].Event.Id == eventId)
                {
                    return EventDefinitions[i];
                }
            }

            return null;
        }
    }
}
