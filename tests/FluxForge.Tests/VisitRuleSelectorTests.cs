using System;
using FluxForge.Story;
using Xunit;

namespace FluxForge.Tests
{
    public sealed class VisitRuleSelectorTests
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
            public bool HasFlag(string key) => false;
            public void MarkFlag(string key) { }
            public bool HasTag(string tag) => false;
            public void SetTag(string tag, bool enabled) { }
            public int SeenCount(string eventId) => 0;
            public void MarkSeen(string eventId) { }
        }

        private static VisitRule Rule(
            string id,
            int priority,
            double chance,
            int cooldownDays,
            Func<VisitContext, bool> condition = null)
        {
            return new VisitRule(
                id,
                Npc.Identifier,
                priority,
                chance,
                cooldownDays,
                VisitKind.Dialogue,
                condition ?? (_ => true),
                null);
        }

        private static VisitContext Context(State state, int day)
        {
            return new VisitContext(Npc, state, day);
        }

        [Fact]
        public void SelectsHighestPriorityEligibleRule()
        {
            VisitDecision decision = VisitRuleSelector.Select(
                new[]
                {
                    Rule("daily", 10, 1d, 0),
                    Rule("special", 100, 1d, 0),
                },
                Context(new State(), 5),
                () => 1d);

            Assert.Equal("special", decision.Rule.Id);
        }

        [Fact]
        public void ChanceFailureDoesNotFallBack()
        {
            VisitDecision decision = VisitRuleSelector.Select(
                new[]
                {
                    Rule("rare", 100, 0.25d, 0),
                    Rule("daily", 10, 1d, 0),
                },
                Context(new State(), 5),
                () => 0.75d);

            Assert.Null(decision);
        }

        [Fact]
        public void CooldownSkipsRule()
        {
            State state = new State { LastVisitDay = 10 };

            VisitDecision decision = VisitRuleSelector.Select(
                new[] { Rule("weekly", 10, 1d, 7) },
                Context(state, 15),
                () => 0d);

            Assert.Null(decision);
        }

        [Fact]
        public void ConditionFailureSkipsRule()
        {
            VisitDecision decision = VisitRuleSelector.Select(
                new[] { Rule("special", 100, 1d, 0, _ => false) },
                Context(new State(), 5),
                () => 0d);

            Assert.Null(decision);
        }
    }
}
