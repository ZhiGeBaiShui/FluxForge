using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品离开容器的钩子：<see cref="GameItem.RemoveParent"/>。
    ///
    /// 与 <see cref="ItemAttached"/> 对称：放下时登记 anchor 格，拿起时注销。
    /// 在 Prefix 里做，因为 <c>RemoveParent</c> 只把 <c>parentInventory</c> 清 0
    /// （ISIL：<c>mov qword ptr [rcx+190h], 0</c>），本身没有别的副作用，
    /// 不需要等它返回。
    ///
    /// 生命周期 #3（物品离开网格），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), "RemoveParent")]
    internal static class ItemDetached
    {
        public static void Prefix(GameItem __instance, GraphNodeStorage node)
        {
            GameGridInventory inventory = node as GameGridInventory;
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
