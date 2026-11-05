using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 冷阱适配器：四个装置配置 tag + 原版液体容器容量 tag 构造装置；
    /// 抽提存量是整份 <see cref="FlowProperties"/>，按属性注册表逐槽读写。
    /// </summary>
    [DeviceAdapter("cold_trap")]
    public sealed class ColdTrapStateAdapter : DeviceStateAdapter<ColdTrapDevice>
    {
        /// <summary>按配置 tag 与容量构造装置；容量来自原版液体容器身份。</summary>
        protected override ColdTrapDevice CreateDevice(GameItem item)
        {
            return new ColdTrapDevice(
                GameItemTags.ReadDouble(item, FlowTags.TemperatureDelta),
                GameItemTags.ReadDouble(item, FlowTags.ColdTrapPhaseThreshold),
                GameItemTags.ReadInt(item, FlowTags.ColdTrapBaseExtraction),
                GameItemTags.ReadDouble(item, FlowTags.ColdTrapEfficiency),
                GameItemTags.ReadInt(item, FlowTags.LiquidContainerCapacity));
        }

        /// <summary>整体替换已有装置的抽提存量，不重建装置。</summary>
        protected override void LoadDevice(GameItem item, ColdTrapDevice device)
        {
            device.ReplaceExtracted(ReadStock(item));
        }

        /// <summary>写回全部属性槽；液体按 int、标量按 double，返回是否有值实际变化。</summary>
        protected override bool SaveDevice(GameItem item, ColdTrapDevice device)
        {
            bool changed = false;
            FlowProperties stock = device.Extracted;

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
    }
}
