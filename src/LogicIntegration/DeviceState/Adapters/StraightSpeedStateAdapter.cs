namespace FluxForge.Mod
{
    /// <summary>直通变速装置适配器：两个额度是 items.json 的静态配置，只读不写回。</summary>
    [DeviceAdapter("straight_speed")]
    public sealed class StraightSpeedStateAdapter : DeviceStateAdapter<StraightSpeedDevice>
    {
        protected override StraightSpeedDevice CreateDevice(GameItem item)
        {
            return new StraightSpeedDevice(
                GameItemTags.ReadDouble(item, FlowTags.StraightSpeedChange),
                GameItemTags.ReadDouble(item, FlowTags.StraightSpeedMax),
                GameItemTags.ReadDouble(item, FlowTags.StraightSpeedMin));
        }

        protected override bool SaveDevice(GameItem item, StraightSpeedDevice device)
        {
            return false;
        }
    }
}
