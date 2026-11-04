using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// 原版只有少数液体会蒸发。表只保留真正有沸点的条目，
    /// 不存在于表里的属性表示“不蒸发”，避免 36 个槽位填 0 的歧义。
    /// </summary>
    public static class FlowBoilingTable
    {
        private static readonly KeyValuePair<FlowPropertyKey, double>[] _entries =
        {
            new KeyValuePair<FlowPropertyKey, double>(FlowProps.Water, 100.0d),
            new KeyValuePair<FlowPropertyKey, double>(FlowProps.Alcohol, 78.37d),
            new KeyValuePair<FlowPropertyKey, double>(FlowProps.LipobondAgent, 55.0d),
        };

        public static IReadOnlyList<KeyValuePair<FlowPropertyKey, double>> Entries
        {
            get { return _entries; }
        }
    }
}
