using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 快照刷新后，用最新 tag 回灌装置适配器（只读，不写回）。
    ///
    /// 游戏侧直接改 tag（倒水、溶解、环境降温）不会通知装置；
    /// <see cref="GameItem.ValidateShapeState"/> 重算 modifiedState 后，
    /// 这里立即把新值灌回已有装置。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ValidateShapeState))]
    public static class DeviceStateReconcilePatch
    {
        public static void Postfix(GameItem __instance)
        {
            DeviceHost host = DeviceRegistry.GetHost(__instance);
            host?.LoadAll();

            // AddParent 后原版才校正 modifiedShape，普通物品占位也要跟随最终形状。
            if (__instance.parentInventory is GameGridInventory inventory)
                GridRegistry.Get(inventory)?.Grid.RefreshRegistered(__instance);

            // 电池充电/放电刷新的是子物品，不是机器本身。
            // 沿原版图关系找到拥有槽位的机器，回灌自定义电池映射。
            if (__instance.IsTag(PowerHelper.POWER_SOURCE_ITEM_ENERGY)
                && __instance.parentInventory != null)
            {
                GameItem machine = GraphUtils.FindParentType<GameItem>(__instance.parentInventory);
                if (machine != null && machine != __instance)
                    DeviceRegistry.GetHost(machine)?.LoadAll();
            }
        }
    }
}
