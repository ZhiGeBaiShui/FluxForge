using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 排序后的格表重建。
    ///
    /// 原版排序会成批重写 <c>modifiedShape</c>，再逐件 <c>Expel</c> →
    /// <c>UncheckedAccept</c> 重放挂载。增量的 AddParent / RemoveParent 钩子虽然
    /// 会被调到，但 <c>UncheckedAccept</c> 是在 <c>AddParent</c> 之后才
    /// <c>ValidateShapeState</c>，登记时拿到的形状可能不是最终形状。排序收尾
    /// 统一 Rebuild 一次，保证格表与最终位置一致。
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
