namespace FluxForge.Mod
{
    [DeviceAdapter("straight")]
    public sealed class StraightStateAdapter : DeviceStateAdapter<StraightDevice>
    {
        protected override StraightDevice CreateDevice(GameItem item) => new StraightDevice();
    }
}
