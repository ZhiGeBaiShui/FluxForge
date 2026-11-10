using System;
using System.Collections.Generic;

namespace FluxForge.Story
{
    /// <summary>
    /// 到访规则选择：条件成立且冷却结束后，取优先级最高的一条；
    /// 选中后只判定一次 Chance，失败不回退低优先级规则。
    /// </summary>
    internal static class VisitRuleSelector
    {
        internal static VisitDecision Select(
            IReadOnlyList<VisitRule> rules,
            VisitContext context,
            Func<double> roll)
        {
            VisitRule selected = null;

            for (int i = 0; i < rules.Count; i++)
            {
                VisitRule rule = rules[i];
                if (rule.NpcId != context.Definition.Identifier)
                {
                    continue;
                }

                if (!rule.Condition(context))
                {
                    continue;
                }

                int lastVisitDay = context.State.LastVisitDay;
                if (rule.CooldownDays > 0
                    && lastVisitDay >= 0
                    && context.Day - lastVisitDay < rule.CooldownDays)
                {
                    continue;
                }

                if (selected == null || rule.Priority > selected.Priority)
                {
                    selected = rule;
                }
            }

            if (selected == null)
            {
                return null;
            }

            if (selected.Chance < 1d && roll() >= selected.Chance)
            {
                return null;
            }

            return new VisitDecision(selected);
        }
    }
}
