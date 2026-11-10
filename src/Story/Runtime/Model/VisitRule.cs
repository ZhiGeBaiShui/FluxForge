using System;

namespace FluxForge.Story
{
    /// <summary>
    /// 一条到访规则：决定 NPC 今天来不来、以什么身份来。
    /// Chance 是 0..1；CooldownDays 是两次到访之间的最小天数。
    /// </summary>
    internal sealed class VisitRule
    {
        internal string Id { get; }
        internal string NpcId { get; }
        internal int Priority { get; }
        internal double Chance { get; }
        internal int CooldownDays { get; }
        internal VisitKind Kind { get; }
        internal Func<VisitContext, bool> Condition { get; }
        internal Action OnVisitStarted { get; }

        internal VisitRule(
            string id,
            string npcId,
            int priority,
            double chance,
            int cooldownDays,
            VisitKind kind,
            Func<VisitContext, bool> condition,
            Action onVisitStarted)
        {
            Id = id;
            NpcId = npcId;
            Priority = priority;
            Chance = chance;
            CooldownDays = cooldownDays;
            Kind = kind;
            Condition = condition;
            OnVisitStarted = onVisitStarted;
        }
    }
}
