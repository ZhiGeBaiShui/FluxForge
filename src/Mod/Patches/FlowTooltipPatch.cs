using System.Collections.Generic;
using HarmonyLib;
using MelonLoader;

namespace ProbablyBagMod.Mod
{
    /// <summary>
    /// tooltip 分派器（patch）：对应原版 <c>GameItemElement.GetTooltipBasic()</c> 里
    /// 那一长串 "xxxHelper.CreateXxxTooltip(builder, item)" 调用。
    ///
    /// 原版是硬编码列出所有 helper，我们没法改游戏源码，所以用 patch 在它之后
    /// 遍历自己的 helper 登记表。<b>判断逻辑全部在 helper 内部</b>，这里只负责分派，
    /// 与「新增功能 = 新增 helper」的原版结构一致。
    /// </summary>
    [HarmonyPatch(typeof(Il2Cpp.GameItemElement), "GetTooltipBasic")]
    public static class FlowTooltipPatch
    {
        /// <summary>已处理过的 builder 指针（同一 builder 只追加一次）。</summary>
        private static readonly HashSet<System.IntPtr> _seen = new HashSet<System.IntPtr>();

        /// <summary>首次触发标记（确认 patch 真的挂上了）。</summary>
        private static bool _firstHitLogged;

        public static void Postfix(Il2Cpp.GameItemElement __instance, ref Il2Cpp.RichTextBuilder __result)
        {
            if (__instance == null || __result == null)
            {
                return;
            }

            // 实测 GetTooltipBasic 每帧被调两次（GetTooltip / GetTooltipAdvanced 各一次），
            // 各自 new 一个 builder。按 builder 指针去重：
            //   不同 builder → 各自追加（两个 tooltip 都完整）
            //   同一 builder → 只追加一次（不重复）
            if (!_seen.Add(__result.Pointer))
            {
                return;
            }

            TrimSeen();

            if (!_firstHitLogged)
            {
                _firstHitLogged = true;
                MelonLogger.Msg("[tooltip] 首次触发 patch=ok id=" + __instance.identifier
                    + " helper=" + FlowTooltipHelpers.Count);
            }

            FlowTooltipHelpers.AppendAll(__result, __instance);
        }

        /// <summary>防止 _seen 无限增长（builder 是临时对象，指针会复用/失效）。</summary>
        private static void TrimSeen()
        {
            if (_seen.Count >= 4096)
            {
                _seen.Clear();
            }
        }

        /// <summary>启动自检：确认 helper 已注册（Postfix 是否真被调用需进游戏看首次悬停日志）。</summary>
        public static void LogPatchState()
        {
            FlowTooltipHelpers.EnsureInitialized();

            MelonLogger.Msg("[tooltip] 分派器就绪，已注册 helper=" + FlowTooltipHelpers.Count
                + "（首次悬停时会打印 [tooltip] 首次触发）");
        }
    }
}
