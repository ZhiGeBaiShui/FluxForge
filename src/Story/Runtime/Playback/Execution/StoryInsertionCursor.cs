using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>同一插入点按优先级逐个考察，每个候选开始前读取当前业务状态。</summary>
    internal sealed class StoryInsertionCursor
    {
        private readonly List<StoryEventDefinition> _candidates
            = new List<StoryEventDefinition>();
        private readonly StoryPlaybackContext _playback;
        private readonly StoryContext _context;
        private int _position;

        internal StoryInsertionCursor(StoryPlaybackContext playback, StoryContext context, string point)
        {
            _playback = playback;
            _context = new StoryContext(context, point);
            foreach (StoryEventDefinition definition in playback.Content.EventDefinitions)
            {
                StoryEvent candidate = definition.Event;
                if (candidate.Trigger != StoryTrigger.InsertionPoint || candidate.Point != point)
                {
                    continue;
                }

                // 优先级高者在前；严格大于才前移，因此相同优先级保留注册顺序。
                int position = _candidates.Count;
                while (position > 0 && candidate.Priority > _candidates[position - 1].Event.Priority)
                {
                    position--;
                }
                _candidates.Insert(position, definition);
            }
        }

        internal StoryEventDefinition Next()
        {
            while (_position < _candidates.Count)
            {
                StoryEventDefinition candidate = _candidates[_position++];
                if (!StoryEventSelector.IsEligible(
                    candidate.Event, _context, _playback.ConsumedGroups))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(candidate.Event.ExclusionGroup))
                {
                    _playback.ConsumedGroups.Add(candidate.Event.ExclusionGroup);
                }

                return candidate;
            }

            return null;
        }
    }
}
