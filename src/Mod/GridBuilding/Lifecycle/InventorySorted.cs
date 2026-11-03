using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 排序后的格表重建。
    ///
    /// <c>InventorySortHelper.Sort</c> 是原版唯一会批量改物品位置、却不通知外部的路径：
    /// 它对每件物品 <c>new GridShapeBuilder(old)</c> → <c>SetTransform(...)</c> →
    /// 直接写 <c>[item+0x1A0]</c>（modifiedShape 的 backing field），
    /// 全程不调 <c>AddParent</c> / <c>RemoveParent</c>，也不调任何 <c>Validate</c>。
    ///
    /// 所以增量路径（ItemAttached / ItemDetached）收不到信号，
    /// 只能在排序结束后对整个网格做一次全量重建。
    ///
    /// 生命周期 #4（排序），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(InventorySortHelper), "Sort")]
    internal static class InventorySorted
    {
        public static void Postfix(GameGridInventory inventory, bool __result)
        {
            // false = 这次排序没有发生。
            if (!__result)
            {
                return;
            }

            FlowRuntime runtime = GridRegistry.Get(inventory);
            if (runtime == null)
            {
                return;
            }

            runtime.Grid.Rebuild();
        }
    }
}
