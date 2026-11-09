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
    /// 装置绑定不按容器类型过滤；网格登记覆盖装置与普通物品。
    ///
    /// 注意时序：原版 UncheckedAccept 在 AddParent 之后才 ValidateShapeState，
    /// 这里读到的是挂载瞬间的形状；排序 / 读档这类批量路径结束后仍要 Rebuild 校正。
    ///
    /// 生命周期 #2（物品进入网格），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(GameItem), "AddParent")]
    public static class ItemAttached
    {
        public static void Postfix(GameItem __instance, bool __result)
        {
            // false = 这次挂载没有发生（已有父容器 / node 为空 / 类型不符），
            // 物品并没有进入任何容器，不该触发绑定。
            if (!__result)
            {
                return;
            }

            // 有装置则绑定；普通物品同样要登记占位，不能提前返回。
            ItemDeviceBinding.Bind(__instance);

            // 落到网格：登记装置 anchor 或普通物品占位。
            GameGridInventory inventory = __instance.parentInventory as GameGridInventory;
            if (inventory == null)
            {
                return;
            }

            FlowRuntime runtime = GridRegistry.Get(inventory);
            if (runtime == null)
            {
                // 构造期登记已完成；不变量强校验尚未落地，这里仍保留兜底注册：
                // 查不到说明登记被退役或整体清空，而网格还在接收挂载。
                runtime = GridRegistry.Register(inventory);
            }

            runtime.Grid.Register(__instance);
        }
    }
}
