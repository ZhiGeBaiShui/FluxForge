using System.Collections.Generic;

namespace FluxForge.Mod
{
    /// <summary>
    /// 冰块装置适配器：水量走 int tag，温度走 double tag。
    ///
    /// Create 按 tag 直接构造装置；Load / Save 由字段表驱动，不重建装置。
    /// </summary>
    [DeviceAdapter("ice_block")]
    public sealed class IceBlockStateAdapter : DeviceStateAdapter<IceBlockDevice>
    {
        /// <summary>成员映射；适配器本身无实例字段。</summary>
        private static readonly TagField<IceBlockDevice>[] Fields =
        {
            TagFields.Int<IceBlockDevice>(
                FlowTagNaming.Water,
                device => device.Water,
                (device, value) => device.Water = value),

            TagFields.Double<IceBlockDevice>(
                FlowTagNaming.ContainerTemperature,
                device => device.Temperature,
                (device, value) => device.Temperature = value),
        };

        /// <inheritdoc/>
        protected override IReadOnlyList<TagField<IceBlockDevice>> FieldMap
        {
            get { return Fields; }
        }

        /// <summary>水量按 int tag 读取，温度按 double tag 读取。</summary>
        protected override IceBlockDevice CreateDevice(GameItem item)
        {
            return new IceBlockDevice(
                GameItemTags.ReadInt(item, FlowTagNaming.Water),
                GameItemTags.ReadDouble(item, FlowTagNaming.ContainerTemperature));
        }
    }
}
