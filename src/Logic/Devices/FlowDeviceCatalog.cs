using System;
using System.Collections.Generic;

namespace FluxForge
{
    /// <summary>
    /// Logic 装置目录：items.json 的 device key → 无参工厂。
    ///
    /// 只存委托、不存实例；key 大小写不敏感。新增装置 = 实现 Logic 类 + 本目录加一行
    /// + items.json 的 device 字段。
    ///
    /// 工厂返回 <see cref="object"/> 而不是某个接口：装置只按自己承担的角色实现
    /// <see cref="IFlowRouter"/> / <see cref="IFlowDevice"/>，是否有状态、是否播报事件
    /// 由装置自己决定，目录不做约束。
    /// </summary>
    public static class FlowDeviceCatalog
    {
        private static readonly Dictionary<string, Func<object>> Factories
            = new Dictionary<string, Func<object>>(StringComparer.OrdinalIgnoreCase)
        {
            ["collector"] = () => new CollectorDevice(),
            ["ice_block"] = () => new IceBlockDevice(),
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
        public static object Create(string key)
        {
            return Factories[key]();
        }
    }
}
