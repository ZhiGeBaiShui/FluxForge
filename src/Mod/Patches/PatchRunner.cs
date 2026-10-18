using System;
using HarmonyLib;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// 补丁安装器：逐个装、逐个记录成败。
    ///
    /// 不用 <c>HarmonyInstance.PatchAll()</c> 的原因是一次实测事故：
    /// <c>GameInventory.UncheckedAccept</c> 是抽象方法（反编译里 "No ISIL was generated"），
    /// Harmony 对它做原生 detour 时目标地址为 0，抛 AccessViolationException，
    /// **整个进程当场死亡，连一行日志都来不及写**。
    ///
    /// 逐个安装后，单个补丁失败只会记一条 Error，其余功能和 UI 照常启动，
    /// 也能在日志里直接看到是哪一个补丁挂不上——比对着崩溃弹窗盲猜强得多。
    /// </summary>
    public static class PatchRunner
    {
        /// <summary>
        /// 全部补丁，按依赖顺序排列（tooltip 与贴图最先，它们决定物品能不能正常显示）。
        /// 新增补丁类时记得加到这里，否则不会被安装。
        /// </summary>
        private static readonly Type[] PatchTypes =
        {
            typeof(FlowTooltipPatch),
            typeof(LoadFromAtlasPatch),
        };

        /// <summary>安装全部补丁，返回成功数。失败的会打 Error 并继续。</summary>
        public static int ApplyAll(HarmonyLib.Harmony harmony)
        {
            int applied = 0;

            for (int i = 0; i < PatchTypes.Length; i++)
            {
                Type type = PatchTypes[i];

                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    applied++;
                }
                catch (Exception e)
                {
                    MelonLogger.Error("[补丁] " + type.Name + " 安装失败：" + e.Message);
                }
            }

            MelonLogger.Msg("[补丁] 已安装 " + applied + "/" + PatchTypes.Length + " 组");
            return applied;
        }
    }
}
