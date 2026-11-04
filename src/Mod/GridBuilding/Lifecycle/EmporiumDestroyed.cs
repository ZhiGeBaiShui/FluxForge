using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 商城主场景销毁时的最后兜底。
    ///
    /// 正常退出（<see cref="SaveExited"/> 的 Escape / QuitToMenu 两条路径）也会走
    /// <see cref="FlowDriver.Shutdown"/>；这里覆盖其他直接销毁 EmporiumEntry 的
    /// 路径（场景重载、直接销毁等），保证 mod 的静态登记表不会跨场景残留。
    ///
    /// 原版不会逐个调用 <see cref="GameItemElement.Destroy"/>：<c>EmporiumEntry.ResetData</c>
    /// 只销毁商城主对象，GameItem 是普通 C# 对象，靠失去引用后 GC。因此这里
    /// 不做逐物品清理，只清 mod 侧的静态表与视图。
    ///
    /// <see cref="FlowDriver.Shutdown"/> 可重复调用，与 SaveExited 的路径先后命中
    /// 也不会出错。
    /// </summary>
    [HarmonyPatch(typeof(EmporiumEntry), "OnDestroy")]
    public static class EmporiumDestroyed
    {
        private static void Postfix(EmporiumEntry __instance)
        {
            // 重复实例在 Awake 里就被 Destroy(this)，而 Instance 仍指向真正的实例：
            // 它的 OnDestroy 不代表当前存档结束，不能跟着清全局表。
            if (EmporiumEntry.Instance != __instance)
            {
                return;
            }

            FlowDriver.Shutdown();
        }
    }
}
