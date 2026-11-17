using HarmonyLib;

namespace FluxForge.Story
{
    /// <summary>在菜单退出入口清理活动剧情；原版随后在协程中重置存档并切换场景。</summary>
    [HarmonyPatch(typeof(EscapeUIManager), "OnMainMenu")]
    internal static class StoryMainMenuExitPatch
    {
        internal static void Prefix()
        {
            StoryNpcRuntime.Shutdown();
        }
    }

    /// <summary>在此返回菜单入口调用 CloseAllScenes 之前清理活动剧情。</summary>
    [HarmonyPatch(typeof(GameMaster), "QuitToMenu")]
    internal static class StoryQuitToMenuPatch
    {
        internal static void Prefix()
        {
            StoryNpcRuntime.Shutdown();
        }
    }
}
