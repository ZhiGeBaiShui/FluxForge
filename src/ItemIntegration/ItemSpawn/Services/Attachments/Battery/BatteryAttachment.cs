using System;

namespace FluxForge.Mod
{
    /// <summary>只建立电池面板；槽位和其中物品的存档由原版 contentWindow 负责。</summary>
    public sealed class BatteryAttachment : ItemAttachment
    {
        public override string Name => "battery";

        public override void Attach(GameItem item, FlowItemDefinition def)
        {
            var slot = new GameSlotInventory();
            item.SetContentWindow(MachineHelper.CreateMachineryWindow(
                true, MachineHelper.PowerPanel(slot)));
            MachineHelper.SetupBatterySlot(slot, item);
            // 当前只支持有实际电量 tag 的普通电池，不接受转换器等虚拟供电物品。
            slot.mayInventoryAddItemFunc = (battery, _) =>
                GeneralHelper.IsItemOwned(battery)
                && battery.IsTag(PowerHelper.POWER_SOURCE_ITEM_TAG)
                && battery.IsTag(PowerHelper.POWER_SOURCE_ITEM_ENERGY);
            slot.onSlotAddItemEndFunc += (_, __, ___) => Refresh(item);
            slot.onSlotRemoveItemEndFunc += (_, __, ___) => Refresh(item);
            item.onLoaded += Refresh;
        }

        private static void Refresh(GameItem item)
        {
            // 回调也会在构造/恢复子物品时触发；Host 尚未建立时无需提前创建。
            DeviceRegistry.GetHost(item)?.LoadAll();
        }
    }
}
