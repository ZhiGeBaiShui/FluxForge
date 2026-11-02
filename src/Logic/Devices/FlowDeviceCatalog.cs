using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// Logic 装置目录：items.json 的 device key → 无参工厂，工厂返回 <see cref="IFlowNotifier"/>。
    ///
    /// 只存委托、不存实例；key 大小写不敏感。新增装置 = 实现 Logic 类 + 本目录加一行
    /// + items.json 的 device 字段。工厂返回 <see cref="IFlowNotifier"/>，因此每个装置都必须
    /// 自己表达「有没有状态」——没有就用空实现，而不是让调用方去判断。
    /// </summary>
    public static class FlowDeviceCatalog
    {
        private static readonly Dictionary<string, Func<IFlowNotifier>> Factories
            = new Dictionary<string, Func<IFlowNotifier>>(StringComparer.OrdinalIgnoreCase)
        {
            ["collector"] = () => new CollectorDevice(),
            ["ice_block"] = () => new IceBlockDevice(),
            ["emitter"] = () => new EmitterDevice(),
            ["turn"] = () => new TurnDevice(),
            ["l_right_down"] = () => new LRightDownDevice(),
            ["t_junction"] = () => new TJunctionDevice(),
            ["double_emit"] = () => new DoubleEmitDevice(),
        };

        /// <summary>已注册的 key。</summary>
        public static IReadOnlyCollection<string> Keys
        {
            get { return Factories.Keys; }
        }

        /// <summary>按 key 创建一台新装置。key 由 items.json 保证合法。</summary>
        public static IFlowNotifier Create(string key)
        {
            return Factories[key]();
        }
    }
}
