using System;
using System.Collections.Generic;
using FluxForge.Story;
using Xunit;

namespace FluxForge.Tests
{
    public sealed class StoryEventSelectorTests
    {
        private static readonly NpcDefinition Npc = new NpcDefinition(
            "npc",
            "NPC",
            "sprite",
            null,
            false);

        private sealed class State : IStoryState
        {
            public int VisitCount { get; set; }
            public int LastVisitDay { get; set; } = -1;
            public Dictionary<string, int> Seen { get; } =
                new Dictionary<string, int>();
            public HashSet<string> Flags { get; } = new HashSet<string>();
            public HashSet<string> Tags { get; } = new HashSet<string>();

            public bool HasFlag(string key) => Flags.Contains(key);
            public void MarkFlag(string key) => Flags.Add(key);
            public bool HasTag(string tag) => Tags.Contains(tag);
            public void SetTag(string tag, bool enabled)
            {
                if (enabled) Tags.Add(tag); else Tags.Remove(tag);
            }

            public int SeenCount(string eventId)
            {
                int count;
                return Seen.TryGetValue(eventId, out count) ? count : 0;
            }

            public void MarkSeen(string eventId)
            {
                Seen[eventId] = SeenCount(eventId) + 1;
            }
        }

        private static StoryEvent Event(
            string id,
            int priority,
            int maxTimes = 0,
            string exclusionGroup = null,
            Func<StoryContext, bool> condition = null,
            StoryTrigger trigger = StoryTrigger.VisitStarted)
        {
            return new StoryEvent(
                id,
                Npc.Identifier,
                trigger,
                priority,
                maxTimes,
                exclusionGroup,
                condition ?? (_ => true));
        }

        private static StoryContext Context(
            State state,
            StoryTrigger trigger = StoryTrigger.VisitStarted)
        {
            return new StoryContext(
                Npc,
                state,
                trigger,
                VisitKind.Dialogue,
                5,
                null);
        }

        [Fact]
        public void SelectsHighestPriorityMatchingEvent()
        {
            StoryEvent selected = StoryEventSelector.Select(
                new[] { Event("daily", 10), Event("first", 100) },
                Context(new State()),
                new HashSet<string>());

            Assert.Equal("first", selected.Id);
        }

        [Fact]
        public void MaxTimesSkipsSeenEvent()
        {
            State state = new State();
            state.MarkSeen("first");

            StoryEvent selected = StoryEventSelector.Select(
                new[] { Event("first", 100, 1), Event("daily", 10) },
                Context(state),
                new HashSet<string>());

            Assert.Equal("daily", selected.Id);
        }

        [Fact]
        public void ConsumedExclusionGroupSkipsEvent()
        {
            StoryEvent selected = StoryEventSelector.Select(
                new[] { Event("injured", 100, 0, "health") },
                Context(new State()),
                new HashSet<string> { "health" });

            Assert.Null(selected);
        }

        [Fact]
        public void DifferentTriggerDoesNotMatch()
        {
            StoryEvent selected = StoryEventSelector.Select(
                new[] { Event("trade", 100, trigger: StoryTrigger.TradeCompleted) },
                Context(new State()),
                new HashSet<string>());

            Assert.Null(selected);
        }

        [Fact]
        public void ConditionCanReadTradeResult()
        {
            StoryContext context = new StoryContext(
                Npc,
                new State(),
                StoryTrigger.TradeCompleted,
                VisitKind.Dialogue,
                5,
                new StoryTradeResult(true, 12, "acid"));

            StoryEvent selected = StoryEventSelector.Select(
                new[]
                {
                    Event(
                        "successful_trade",
                        100,
                        trigger: StoryTrigger.TradeCompleted,
                        condition: ctx => ctx.TradeResult.Success),
                },
                context,
                new HashSet<string>());

            Assert.Equal("successful_trade", selected.Id);
        }
    }
}
