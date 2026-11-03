using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 拿起物品的钩子：<see cref="ItemMouseDragHandler.StartDrag"/>。
    ///
    /// 拿起时游戏**不会**调 <see cref="GameItem.RemoveParent"/>——物品只是视觉上跟随鼠标，
    /// <c>parentInventory</c> 仍指向原网格。所以必须在拿起时就注销，
    /// 否则流源会继续撞到「已经被拿在手上」的装置。
    ///
    /// 松手时 <c>EndDrag</c> 会走 RemoveParent + AddParent，
    /// <see cref="ItemDetached"/> 与 <see cref="ItemAttached"/> 会再各触发一次。
    ///
    /// 生命周期 #3 的补充（拿起），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(ItemMouseDragHandler), "StartDrag")]
    internal static class ItemPickedUp
    {
        public static void Postfix(ItemMouseDragHandler __instance)
        {
            GameItem item = __instance.GetCurrentItem();
            if (item == null)
            {
                return;
            }

            GameGridInventory inventory = item.parentInventory as GameGridInventory;
            if (inventory == null)
            {
                return;
            }

            FlowRuntime runtime = GridRegistry.Get(inventory);
            if (runtime == null)
            {
                return;
            }

            runtime.Grid.Unregister(item);
        }
    }
}
