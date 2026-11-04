using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 快照刷新后，用最新 tag 回灌装置适配器（只读，不写回）。
    ///
    /// 游戏侧直接改 tag（倒水、溶解、环境降温）不会通知装置；
    /// <see cref="GameItem.ValidateShapeState"/> 重算 modifiedState 后，
    /// 这里把新值灌回 <see cref="DeviceStateSync"/> 管理的适配器。
    /// 装置自己的变化仍由 <see cref="DeviceStateSync.Flush"/> 在帧末回写。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.ValidateShapeState))]
    public static class DeviceStateReconcilePatch
    {
        public static void Postfix(GameItem __instance)
        {
            DeviceHost host = DeviceRegistry.GetHost(__instance);
            if (host == null || host.IsDirty)
            {
                return;
            }

            DeviceStateSync.Load(host);
        }
    }
}
