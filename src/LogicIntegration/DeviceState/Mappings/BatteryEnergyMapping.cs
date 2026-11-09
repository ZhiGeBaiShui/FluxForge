using System;

namespace FluxForge.Mod
{
    /// <summary>自定义映射示例：源是机器子槽里的电池，不是机器 tag。
    /// 无状态，不缓存电池引用。Load 读取电量，Save 同步写回逻辑层的剩余电量。
    /// </summary>
    public sealed class BatteryEnergyMapping<TDevice> where TDevice : class
    {
        private readonly Action<TDevice, int> _load;
        private readonly Func<TDevice, int> _get;

        public BatteryEnergyMapping(Action<TDevice, int> load,
            Func<TDevice, int> get)
        {
            _load = load;
            _get = get;
        }

        public void Load(GameItem machine, TDevice device)
        {
            var battery = MachineHelper.GetBatterySlot(machine)?.childItem;
            _load(device, battery == null ? 0 :
                GameItemTags.ReadInt(battery, PowerHelper.POWER_SOURCE_ITEM_ENERGY));
        }

        public bool Save(GameItem machine, TDevice device)
        {
            var battery = MachineHelper.GetBatterySlot(machine)?.childItem;
            if (battery == null) return false;
            if (GameItemTags.WriteInt(battery, PowerHelper.POWER_SOURCE_ITEM_ENERGY, _get(device)))
            {
                ItemTagCommit.Commit(battery);
                PowerHelper.UpdateEnergyCreditSprite(battery);
            }
            // 提交的是电池，不需要再次提交机器自身。
            return false;
        }
    }
}
