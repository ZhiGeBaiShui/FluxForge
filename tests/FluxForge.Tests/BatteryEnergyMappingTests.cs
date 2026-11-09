using FluxForge.Mod;
using Xunit;

namespace FluxForge.Tests
{
    public class BatteryEnergyMappingTests
    {
        private static GameItem Machine(int energy)
        {
            var battery = new GameItem();
            battery.state.GetTag(PowerHelper.POWER_SOURCE_ITEM_ENERGY).SetInt(energy);
            battery.SyncModifiedState();
            var machine = new GameItem { BatterySlot = new GameSlotInventory { childItem = battery } };
            machine.SetModifiedTag(DistillerStateAdapter.WaterPerEnergyTag, 10d);
            return machine;
        }

        [Fact]
        public void LoadBeforeChildrenThenLoadRestoredBatteryIntoSameDevice()
        {
            var item = Machine(12);
            var slot = item.BatterySlot;
            item.BatterySlot = null;
            var adapter = new DistillerStateAdapter();
            var device = (DistillerDevice)adapter.Create(item);
            Assert.Equal(0, device.Energy);
            item.BatterySlot = slot;
            adapter.Load(item, device);
            Assert.Equal(12, device.Energy);
            Assert.Equal(10, device.WaterPerEnergy);
        }

        [Fact]
        public void PartialDistillationWritesRemainingEnergyAndRepeatedSaveIsStable()
        {
            var item = Machine(2);
            var adapter = new DistillerStateAdapter();
            var device = (DistillerDevice)adapter.Create(item);
            var mixture = TestFlow.Make(80);
            mixture.Set(FlowProps.HeavyMetal, 20);
            Assert.Equal(20, device.Distill(mixture, 100));
            adapter.Save(item, device);
            Assert.Equal(0, item.BatterySlot.childItem.modifiedState.GetTag(PowerHelper.POWER_SOURCE_ITEM_ENERGY).GetInt());
            adapter.Save(item, device);
            Assert.Equal(0, device.Energy);
        }

        [Fact]
        public void BatteryReplacementAndRemovalRefreshResources()
        {
            var item = Machine(20);
            var adapter = new DistillerStateAdapter();
            var device = (DistillerDevice)adapter.Create(item);
            item.BatterySlot = Machine(4).BatterySlot;
            adapter.Load(item, device);
            Assert.Equal(4, device.Energy);
            item.BatterySlot.childItem = null;
            adapter.Load(item, device);
            Assert.Equal(0, device.Energy);
        }

        [Theory]
        [InlineData(100, 0)]
        [InlineData(40, 60)]
        [InlineData(50, 50)]
        public void BypassDoesNotConsumeBattery(double water, double other)
        {
            var item = Machine(10);
            var adapter = new DistillerStateAdapter();
            var device = (DistillerDevice)adapter.Create(item);
            var mixture = TestFlow.Make(water);
            mixture.Set(FlowProps.HeavyMetal, other);
            Assert.Equal(0, device.Distill(mixture, 100));
            Assert.Equal(10, device.Energy);
        }
    }
}
