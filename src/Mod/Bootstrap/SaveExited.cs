using System.Reflection;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 生命周期 #5：退出存档。
    ///
    /// 钩子是 <c>GameMaster.QuitToMenu</c>——「返回主菜单」才是当前存档真正结束的时刻。
    ///
    /// 不能用 <c>CloseAllScenes</c>：它是「关闭所有场景」，在「开始游戏」时也会被调
    /// （`GameMaster.NewGame` 1051 / `MenuScene` 按钮回调），
    /// 用它做清理会在进入存档后立刻执行一次，把刚注册的网格全部清掉。
    ///
    /// 清空放在这里、而不是读档完成时：
    ///   · 退出时旧 GameItem / 旧网格 / 旧装置一起作废，越早清越干净；
    ///   · 读档完成（<see cref="SaveLoaded"/>）只需要「建」，不需要「清」。
    ///
    /// 完整生命周期表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch]
    internal static class SaveExited
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(GameMaster), "QuitToMenu");
        }

        private static void Postfix()
        {
            FlowDriver.Shutdown();
        }
    }
}
