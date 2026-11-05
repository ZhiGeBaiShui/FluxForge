namespace FluxForge.Mod
{
    /// <summary>温度控制装置适配器：温度是 items.json 的静态配置，只读不写回。</summary>
    [DeviceAdapter("temperature")]
    public sealed class TemperatureConfigAdapter : DeviceStateAdapter<TemperatureDevice>
    {
        /// <summary>按 tag 里的温度增量构造装置。</summary>
        protected override TemperatureDevice CreateDevice(GameItem item)
        {
            return new TemperatureDevice(
                GameItemTags.ReadDouble(item, FlowTagNaming.ContainerTemperature));
        }

        /// <summary>运行期回灌配置值，不重建装置。</summary>
        protected override void LoadDevice(GameItem item, TemperatureDevice device)
        {
            device.Temperature = GameItemTags.ReadDouble(
                item,
                FlowTagNaming.ContainerTemperature);
        }

        /// <summary>温度配置来自 items.json，运行期不回写。</summary>
        protected override bool SaveDevice(GameItem item, TemperatureDevice device)
        {
            return false;
        }
    }
}
