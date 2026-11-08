using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 收集装置适配器：存量是整份 <see cref="FlowProperties"/>，无法用单成员字段表表达，
    /// 因此覆写 Load / Save，按属性注册表逐槽读写。
    /// </summary>
    [DeviceAdapter("collector")]
    public sealed class CollectorStateAdapter : DeviceStateAdapter<CollectorDevice>
    {
        /// <summary>按 tag 读出完整存量构造装置。</summary>
        protected override CollectorDevice CreateDevice(GameItem item)
        {
            return new CollectorDevice(ReadStock(item))
            {
                EmitSpeed = ReadEmitSpeed(item),
            };
        }

        /// <summary>整体替换已有装置的存量，不重建装置。</summary>
        protected override void LoadDevice(GameItem item, CollectorDevice device)
        {
            device.ReplaceStock(ReadStock(item));
            device.EmitSpeed = ReadEmitSpeed(item);
        }

        /// <summary>写回全部属性槽；液体按 int、标量按 double，返回是否有值实际变化。</summary>
        protected override bool SaveDevice(GameItem item, CollectorDevice device)
        {
            bool changed = false;
            FlowProperties stock = device.Stock;

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = stock.Get(property.Key);

                changed |= property.Kind == FlowPropertyKind.Liquid
                    ? GameItemTags.WriteInt(item, property.Tag, (int)value)
                    : GameItemTags.WriteDouble(item, property.Tag, value);
            }

            return changed;
        }

        /// <summary>缺 tag 读作 0，写不进任何未声明的属性。</summary>
        private static FlowProperties ReadStock(GameItem item)
        {
            FlowProperties stock = new FlowProperties();

            foreach (FlowPropertyTag property in FlowPropertyTags.All)
            {
                double value = property.Kind == FlowPropertyKind.Liquid
                    ? GameItemTags.ReadInt(item, property.Tag)
                    : GameItemTags.ReadDouble(item, property.Tag);

                stock.Set(property.Key, value);
            }

            return stock;
        }

        private static double ReadEmitSpeed(GameItem item)
        {
            return GameItemTags.ReadDouble(item, FlowTags.EmitterSpeed);
        }
    }
}
