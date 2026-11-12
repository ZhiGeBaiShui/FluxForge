using System;

namespace FluxForge.Mod
{
    /// <summary>
    /// Mod 物品 attachment 到原版机械窗口组件的中转层。
    /// 机器仍由 Mod 的 ItemSetup 创建；这里只复用原版面板、窗口和电池槽行为。
    /// </summary>
    internal static class MachineryAttachmentFactory
    {
        internal static PixelWindow CreateWindow(params MachineryPanel[] panels)
        {
            return MachineHelper.CreateMachineryWindow(true, panels);
        }

        internal static GameSlotInventory CreateBatterySlot(GameItem machine)
        {
            GameSlotInventory slot = new GameSlotInventory();
            MachineHelper.SetupBatterySlot(slot, machine);
            slot.mayInventoryAddItemFunc = (battery, _) =>
                GeneralHelper.IsItemOwned(battery)
                && battery.IsTag(PowerHelper.POWER_SOURCE_ITEM_TAG)
                && battery.IsTag(PowerHelper.POWER_SOURCE_ITEM_ENERGY);
            return slot;
        }

        internal static bool CanDrawPower(GameSlotInventory batterySlot, int amount)
        {
            GameItem battery = batterySlot.childItem;
            return battery != null && PowerHelper.CanDrawPowerSource(battery, amount);
        }

        internal static bool DrawPower(GameSlotInventory batterySlot, int amount)
        {
            GameItem battery = batterySlot.childItem;
            return battery != null
                && PowerHelper.DrawPowerSource(battery, amount);
        }
    }
}
