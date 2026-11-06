using System.Collections.Generic;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 拖拽收尾时补登记。
    ///
    /// 拿起物品时 <see cref="ItemPickedUp"/> 会撤销格表条目，正常换格依赖
    /// RemoveParent + AddParent 重新登记；但“放回原位”“拖到不可放置处后返回”
    /// 不会触发 AddParent，格表就永久缺这一项。这里在拖拽结束后按最终
    /// parentInventory 做一次幂等 Register。
    /// </summary>
    [HarmonyPatch(typeof(ItemMouseDragHandler), "EndDrag")]
    public static class ItemDragEnded
    {
        public static void Postfix(ItemMouseDragHandler __instance)
        {
            ItemDragRestore.Register(__instance.GetCurrentItem());
        }
    }

    /// <summary>拖拽被中止时，物品留在原格；补一次登记。</summary>
    [HarmonyPatch(typeof(ItemMouseDragHandler), "AbortDrag")]
    public static class ItemDragAborted
    {
        public static void Prefix(ItemMouseDragHandler __instance, out GameItem __state)
        {
            __state = __instance.GetCurrentItem();
        }

        public static void Postfix(GameItem __state)
        {
            ItemDragRestore.Register(__state);
        }
    }

    /// <summary>
    /// 批量拖拽取消 / 失败时，RestoreGhosts 会把 ghost 放回原容器；
    /// 这一路径也不会统一触发 AddParent，所以在恢复后逐个补登记。
    /// </summary>
    [HarmonyPatch(typeof(ItemMultiSelectHandler), "RestoreGhosts")]
    public static class ItemGroupDragRestored
    {
        public static void Postfix(List<GameItemElement> ___selectedItems)
        {
            for (int i = 0; i < ___selectedItems.Count; i++)
            {
                ItemDragRestore.Register(___selectedItems[i]);
            }
        }
    }

    internal static class ItemDragRestore
    {
        /// <summary>只在物品最终仍属于某个网格时重新登记；已销毁/进入普通容器则跳过。</summary>
        public static void Register(GameItem item)
        {
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

            runtime.Grid.Register(item);
        }
    }
}
