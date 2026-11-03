using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 网格出现钩子：网格对象出生或尺寸变化时，确保它有运行环境并登记。
    ///
    /// 一个 patch 类通过 <see cref="TargetMethods"/> 同时覆盖两类目标：
    ///   · 所有公开构造函数：直接给 inventoryShape 赋值，不经过 SetShape。
    ///     Postfix 返回时 handler / shape 都已就绪，FlowRuntime 从这里开始，
    ///     生命周期与 GameGridInventory 对齐。
    ///   · 所有公开 SetShape 重载：运行时扩容 / 换形状。Register 幂等，通常
    ///     命中已有环境直接返回；适配器每帧现读 inventoryShape，所以扩容本身
    ///     不需要重建运行环境。
    ///
    /// 私有无参构造只建 handler / inner / background 等节点，Postfix 时
    /// inventoryShape 还是 null，不能作为登记点；GetConstructors() 本来也只
    /// 返回公开构造。
    ///
    /// 这里直接对 <c>typeof(GameGridInventory)</c> 枚举目标，不把 Type 作为
    /// 参数传给反射辅助方法；避免游戏的 mod scanner 把该调用判定为
    /// "reflection-external on a computed type"。
    ///
    /// 所有目标共用同一个 Postfix；<see cref="GridRegistry.Register"/> 幂等，
    /// 构造函数登记后再走 SetShape 不会重复创建。
    ///
    /// 生命周期 #1（网格出现），完整表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch(typeof(GameGridInventory))]
    internal static class GridCreatedPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            ConstructorInfo[] constructors = typeof(GameGridInventory).GetConstructors();
            if (constructors.Length == 0)
            {
                throw new InvalidOperationException(
                    "[FluxForge] Patch 目标缺失：GameGridInventory 没有公开构造函数");
            }

            for (int i = 0; i < constructors.Length; i++)
            {
                yield return constructors[i];
            }

            List<MethodBase> setShapes = new List<MethodBase>();
            foreach (MethodInfo method in typeof(GameGridInventory).GetMethods(
                BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name == nameof(GameGridInventory.SetShape))
                {
                    setShapes.Add(method);
                }
            }

            if (setShapes.Count == 0)
            {
                throw new InvalidOperationException(
                    "[FluxForge] Patch 目标缺失：GameGridInventory.SetShape");
            }

            for (int i = 0; i < setShapes.Count; i++)
            {
                yield return setShapes[i];
            }
        }

        public static void Postfix(GameGridInventory __instance)
        {
            // 构造与 SetShape 都落到这里：前者是登记点，后者只确认登记还在。
            GridRegistry.Register(__instance);
        }
    }
}
