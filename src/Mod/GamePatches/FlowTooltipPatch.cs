using System.Collections.Generic;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// tooltip 分派器（patch）：对应原版 <c>GameItemElement.GetTooltipBasic()</c> 里
    /// 那一长串 "xxxHelper.CreateXxxTooltip(builder, item)" 调用。
    ///
    /// 原版是硬编码列出所有 helper，我们没法改游戏源码，所以用 patch 在它之后
    /// 遍历自己的 helper 登记表。<b>判断逻辑全部在 helper 内部</b>，这里只负责分派，
    /// 与「新增功能 = 新增 helper」的原版结构一致。
    /// </summary>
    [HarmonyPatch(typeof(GameItemElement), "GetTooltipBasic")]
    public static class FlowTooltipPatch
    {
        /// <summary>已处理过的 builder（同一 builder 只追加一次）。</summary>
        private static readonly HashSet<RichTextBuilder> _seen = new HashSet<RichTextBuilder>();

        public static void Postfix(GameItemElement __instance, ref RichTextBuilder __result)
        {
            if (__instance == null || __result == null)
            {
                return;
            }

            // 实测 GetTooltipBasic 每帧被调两次（GetTooltip / GetTooltipAdvanced 各一次），
            // 各自 new 一个 builder。按 builder 引用去重：
            //   不同 builder → 各自追加（两个 tooltip 都完整）
            //   同一 builder → 只追加一次（不重复）
            if (!_seen.Add(__result))
            {
                return;
            }

            TrimSeen();

            // 这是游戏 tooltip 链上的回调，异常冒出去会破坏本次 UI 刷新；
            // 追加失败最多是这个物品少两行状态，不该让游戏陪葬
            try
            {
                FlowTooltipHelpers.AppendAll(__result, __instance);
            }
            catch (System.Exception e)
            {
                FFModEntry.Log.Error("[tooltip] 追加状态行失败：" + e);
            }
        }

        /// <summary>防止 _seen 无限增长（builder 是临时对象，引用会失效）。</summary>
        private static void TrimSeen()
        {
            if (_seen.Count >= 4096)
            {
                _seen.Clear();
            }
        }
    }
}
