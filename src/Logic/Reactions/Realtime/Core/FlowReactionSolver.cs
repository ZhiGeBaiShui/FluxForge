using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 流源化学反应入口。
    ///
    /// 规则不在这里登记：静态初始化时扫描本程序集，自动实例化所有
    /// <see cref="FlowReactionRule"/> 子类，按 <see cref="FlowReactionRule.Order"/>
    /// 排序后缓存。调用 <see cref="React"/> 时只做一次顺序遍历。
    /// </summary>
    public static class FlowReactionSolver
    {
        private static readonly List<FlowReactionRule> Rules = DiscoverRules();

        /// <summary>对一份流源属性执行全部反应规则。</summary>
        public static void React(FlowProperties properties)
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                Rules[i].React(properties);
            }
        }

        /// <summary>
        /// 扫描本程序集里的全部具体规则类型。
        /// 规则必须有无参构造；构造失败会在静态初始化阶段直接抛出，避免静默跳过。
        /// </summary>
        private static List<FlowReactionRule> DiscoverRules()
        {
            List<FlowReactionRule> rules = new List<FlowReactionRule>();

            Type[] types = typeof(FlowReactionSolver).Assembly.GetTypes();
            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type.IsAbstract || !typeof(FlowReactionRule).IsAssignableFrom(type))
                {
                    continue;
                }

                rules.Add((FlowReactionRule)Activator.CreateInstance(type));
            }

            rules.Sort(CompareRules);
            return rules;
        }

        private static int CompareRules(FlowReactionRule left, FlowReactionRule right)
        {
            int order = left.Order.CompareTo(right.Order);
            if (order != 0)
            {
                return order;
            }

            return string.CompareOrdinal(
                left.GetType().FullName,
                right.GetType().FullName);
        }
    }
}
