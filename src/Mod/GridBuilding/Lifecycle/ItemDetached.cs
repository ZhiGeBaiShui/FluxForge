using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品离开容器的钩子：<see cref="GameItem.RemoveParent"/>。
    ///
    /// 与 <see cref="ItemAttached"/> 对称：进容器时登记 anchor 格，离开时注销。
    /// 在 Prefix 里做，因为 <c>RemoveParent</c> 只把 <c>parentInventory</c> 清 0
    /// （ISIL：<c>mov qword ptr [rcx+190h], 0</c>），本身没有别的副作用，
    /// 不需要等它返回。
    ///
    /// 只动网格格表，不动 <see cref="DeviceRegistry"/>：装置跟着物品走，拿起 /
    /// 换容器后还要继续用；装置登记的整表清理由 FlowDriver.Reset / Shutdown 负责。
    ///
    /// 生命周期 #3（物品离开网格），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), "RemoveParent")]
    internal static class ItemDetached
    {
        public static void Prefix(GameItem __instance)
        {
            // Prefix 阶段 parentInventory 还是旧值（RemoveParent 之后才清 0），
            // 用它判定，比 node 参数可靠：node 是 GraphNodeStorage，未必是 GameGridInventory。
            GameGridInventory inventory = __instance.parentInventory as GameGridInventory;
            if (inventory == null)
            {
                return;
            }

            FlowRuntime runtime = GridRegistry.Get(inventory);
            if (runtime == null)
            {
                return;
            }

            runtime.Grid.Unregister(__instance);
        }
    }
}
