using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 物品挂上父容器的唯一钩子：<see cref="GameItem.AddParent"/>。
    ///
    /// 为什么选它：GameInventory 上的 UncheckedAccept 是 abstract（无方法体），Harmony 打不中；
    /// 而且每个容器子类各写一份实现，GameGridScrollableInventory 还是独立重写的。
    /// AddParent 是 GameItem 上的具体实现，所有容器都会调它，一个补丁覆盖全部。
    ///
    /// 不按容器类型过滤：装置属于物品，不属于它当下待的容器。
    /// 唯一的判据是物品身上有没有装置类型 tag（见 <see cref="ItemDeviceBinding"/>）。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), "AddParent")]
    public static class ItemAttachedPatch
    {
        public static void Postfix(GameItem __instance, bool __result)
        {
            // false = 这次挂载没有发生（已有父容器 / node 为空 / 类型不符），
            // 物品并没有进入任何容器，不该触发绑定。
            if (!__result)
            {
                return;
            }

            ItemDeviceBinding.Bind(__instance);
        }
    }
}
