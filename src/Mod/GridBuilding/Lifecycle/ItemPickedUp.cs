using System.Collections.Generic;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 单件拿起物品的钩子：<see cref="ItemMouseDragHandler.StartDrag"/>。
    ///
    /// 拿起时游戏**不会**调 <see cref="GameItem.RemoveParent"/>——物品只是视觉上跟随鼠标，
    /// <c>parentInventory</c> 仍指向原网格。所以必须在拿起时就注销，
    /// 否则流源会继续撞到「已经被拿在手上」的装置。
    ///
    /// 这里只撤格表条目，装置实例不销毁：松手落回容器时 EndDrag 会走
    /// RemoveParent + AddParent，<see cref="ItemDetached"/> 与
    /// <see cref="ItemAttached"/> 会再各触发一次并重新登记格表。
    /// </summary>
    [HarmonyPatch(typeof(ItemMouseDragHandler), "StartDrag")]
    public static class ItemPickedUp
    {
        public static void Postfix(ItemMouseDragHandler __instance)
        {
            GameItem item = __instance.GetCurrentItem();
            if (item == null)
            {
                return;
            }

            ItemPickedUpGrid.Unregister(item);
        }
    }

    /// <summary>
    /// 批量框选拖拽拿起：<see cref="ItemMultiSelectHandler.StartGroupDrag"/>。
    ///
    /// 与单件 <see cref="ItemPickedUp"/> 对称。批量拖拽不调用
    /// <see cref="GameItem.RemoveParent"/>，也不经过 <see cref="ItemMouseDragHandler"/>，
    /// 所以在这里撤销全部选中物品的格表条目。
    ///
    /// 拖拽结束落点会走 Expel + UncheckedAccept，由
    /// <see cref="ItemDetached"/> / <see cref="ItemAttached"/> 重新登记；
    /// 本 patch 不处理结束/取消。
    /// </summary>
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "StartGroupDrag")]
    public static class ItemGroupPickedUp
    {
        public static void Postfix(List<GameItemElement> ___selectedItems)
        {
            for (int i = 0; i < ___selectedItems.Count; i++)
            {
                GameItem item = ___selectedItems[i];
                if (item == null)
                {
                    continue;
                }

                ItemPickedUpGrid.Unregister(item);
            }
        }
    }

    /// <summary>拿起时从所属网格的格表撤条目；不在网格里的物品直接跳过。</summary>
    internal static class ItemPickedUpGrid
    {
        public static void Unregister(GameItem item)
        {
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
