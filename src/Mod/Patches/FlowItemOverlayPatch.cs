using System;
using FluxForge.Mod.Visual;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格 UI 生命周期钩子：给每块 <see cref="GameGridInventory"/> 挂上物品顶层覆盖层。
    ///
    /// 为什么是构造函数（反编译 Assembly-CSharp 确认）：
    ///   · 所有公开构造 <c>GameGridInventory(int,int)</c> / <c>GameGridInventory(string,int)</c>
    ///     都链到私有构造 <c>GameGridInventory()</c>；
    ///   · 私有构造里 <c>handler = TreeNodeRender.Instantiate(this)</c> 建出网格节点与
    ///     RectTransform，到 postfix 时 <c>inventory.rectTransform</c> 已经可用。
    /// 这是「网格 UI 出现」的唯一入口，比在别的行为上轮询可靠。
    ///
    /// 销毁不 patch：覆盖层节点是网格节点的子节点，网格销毁时 Unity 会一起销毁并回调
    /// <c>FlowItemOverlayLayer.OnDestroy</c>，不需要全局静态表。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory), MethodType.Constructor, new Type[0])]
    public static class FlowItemOverlayPatch
    {
        public static void Postfix(GameGridInventory __instance)
        {
            FlowItemOverlayLayer.Attach(__instance);
        }
    }
}
