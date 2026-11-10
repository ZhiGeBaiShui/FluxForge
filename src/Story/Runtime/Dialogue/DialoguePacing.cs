using System.Runtime.CompilerServices;
using HarmonyLib;

namespace FluxForge.Story
{
    /// <summary>
    /// 为自定义剧情行登记停顿时间。原版 DialogUIManager 默认按全局设置推进，
    /// 这里只覆盖已登记的行，未登记的原版对话不受影响。
    /// </summary>
    internal static class DialoguePacing
    {
        private sealed class Pause
        {
            internal float Seconds;
        }

        private static readonly ConditionalWeakTable<Dialogue, Pause> Pauses
            = new ConditionalWeakTable<Dialogue, Pause>();

        internal static void Set(Dialogue line, float seconds)
        {
            Pauses.Remove(line);
            Pauses.Add(line, new Pause { Seconds = seconds });
        }

        internal static bool TryGet(Dialogue line, out float seconds)
        {
            Pause pause;
            if (Pauses.TryGetValue(line, out pause))
            {
                seconds = pause.Seconds;
                return true;
            }

            seconds = 0f;
            return false;
        }
    }

    [HarmonyPatch(typeof(DialogUIManager), "ScheduleAutoAdvance")]
    internal static class DialogueAutoAdvancePatch
    {
        private static void Prefix(ref float delay)
        {
            DialogUIManager manager = DialogUIManager.Instance;
            if (manager == null || manager.isSkipCurrentDialog)
            {
                return;
            }

            float pause;
            if (DialoguePacing.TryGet(manager.GetCurrentDialog(), out pause))
            {
                delay = pause;
            }
        }
    }
}
