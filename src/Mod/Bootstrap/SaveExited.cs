using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 生命周期 #5：退出存档时清理本 mod 的全部内存引用。
    ///
    /// 覆盖两条真实退出路径，缺一不可：
    ///   · 普通 Escape → 主菜单：<c>EscapeUIManager.OnMainMenu</c>；
    ///   · 团灭 / 调试返主菜单：<c>GameMaster.QuitToMenu</c>。
    ///
    /// 不能用 <c>PlayerStore.Reset</c>：它同时被 <c>PlayerStore</c> 构造函数和
    /// <c>PreLoadStore</c> 调用，会在 <c>EmporiumEntry.Start</c> 构造展示窗 / 柜台
    /// 网格的过程中触发 Shutdown，把刚登记的运行环境清掉。
    ///
    /// 不能用 <c>GameMaster.CloseAllScenes</c>：它同时是「开始游戏 / 继续游戏」的
    /// 路径，会在与退出存档无关的时刻把登记整体清掉。
    ///
    /// 两条路径都必须走 <see cref="FlowDriver.Shutdown"/>：退出存档的目标就是清掉
    /// GridRegistry、DeviceRegistry 和全部视觉视图。下一个存档会重新构造网格，
    /// 由 <see cref="GridCreatedPatch"/> 的构造函数 Postfix 重新注册 FlowRuntime；
    /// 场景级的 <see cref="EmporiumDestroyed"/> 也会再兜一次 Shutdown，重复调用安全。
    ///
    /// 完整生命周期表见 `Lifecycle/README.md`。
    /// </summary>
    [HarmonyPatch]
    internal static class SaveExited
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase mainMenu = AccessTools.Method(typeof(EscapeUIManager), "OnMainMenu");
            if (mainMenu == null)
            {
                throw new InvalidOperationException(
                    "[FluxForge] Patch 目标缺失：EscapeUIManager.OnMainMenu");
            }

            yield return mainMenu;

            MethodBase quit = AccessTools.Method(typeof(GameMaster), "QuitToMenu");
            if (quit == null)
            {
                throw new InvalidOperationException(
                    "[FluxForge] Patch 目标缺失：GameMaster.QuitToMenu");
            }

            yield return quit;
        }

        private static void Postfix()
        {
            FlowDriver.Shutdown();
        }
    }
}
