using System;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格出现钩子：尺寸确定时创建运行环境并登记。
    ///
    /// 为什么是 <see cref="GameGridInventory.SetShape"/> 而不是构造函数（ISIL 取证）：
    ///   · 私有无参构造只建 <c>handler</c> / <c>inner</c> / <c>background</c> 等节点，
    ///     此时 <c>inventoryShape</c> 还是 null；
    ///   · 公开构造 <c>(int,int)</c> / <c>(string,int)</c> 在 <c>this()</c> 之后才调
    ///     <c>SetShape</c>，返回时 <c>[rdi+0x1B0]</c>（inventoryShape）才被赋值。
    ///   · 运行时改尺寸也走同一个方法，登记天然幂等。
    ///
    /// 两个重载各一个补丁类：Harmony 不能在一个类上同时声明两个同名的重载目标。
    ///
    /// 生命周期 #1（网格出现），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory), "SetShape", new Type[] { typeof(int), typeof(int) })]
    public static class GridCreatedPatch
    {
        public static void Postfix(GameGridInventory __instance)
        {
            GridRegistry.Register(__instance);
        }
    }

    [HarmonyPatch(typeof(GameGridInventory), "SetShape", new Type[] { typeof(string), typeof(int) })]
    public static class GridCreatedFromShapePatch
    {
        public static void Postfix(GameGridInventory __instance)
        {
            GridRegistry.Register(__instance);
        }
    }
}
