using System.Reflection;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 生命周期 #5：退出存档。
    ///
    /// 原版在「继续游戏（读档前）」「返回主菜单」「开始新游戏」三条路径上都会先调
    /// <c>GameMaster.CloseAllScenes</c>（`GameMaster.txt` 908 / 1003 / 1051，
    /// 以及 `MenuScene` / `RaidManager` 的按钮回调），是「当前存档结束」的统一入口。
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
            return AccessTools.Method(typeof(GameMaster), "CloseAllScenes");
        }

        private static void Postfix()
        {
            FlowDriver.Reset();
        }
    }
}
