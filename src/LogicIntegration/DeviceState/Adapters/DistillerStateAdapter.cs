namespace FluxForge.Mod
{
    [DeviceAdapter("distiller")]
    public sealed class DistillerStateAdapter : DeviceStateAdapter<DistillerDevice>
    {
        public const string WaterPerEnergyTag = "FLUXFORGE_DISTILLER_WATER_PER_ENERGY";
        private static readonly BatteryEnergyMapping<DistillerDevice> Battery = new(
            (device, energy) => device.LoadEnergy(energy),
            device => device.Energy);

        protected override DistillerDevice CreateDevice(GameItem item)
        {
            var device = new DistillerDevice();
            LoadDevice(item, device);
            return device;
        }

        protected override void LoadDevice(GameItem item, DistillerDevice device)
        {
            device.WaterPerEnergy = GameItemTags.ReadDouble(item, WaterPerEnergyTag);
            Battery.Load(item, device);
        }

        protected override bool SaveDevice(GameItem item, DistillerDevice device)
        {
            return Battery.Save(item, device);
        }
    }
}
