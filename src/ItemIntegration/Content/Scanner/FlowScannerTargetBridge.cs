using System;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 让原版 AquaScan / 化学扫描仪接受流源装置容器。
    ///
    /// 只在扫描仪工厂返回时包装一次目标委托，不遍历库存、不逐帧查找扫描仪。
    /// 高亮走 <see cref="GameItem.MayTarget"/> + <see cref="GameItem.CanTarget"/>，
    /// 因此 may/can 四组委托都必须同步扩展；on 只负责把装置目标分派到流源面板。
    /// </summary>
    internal static class FlowScannerTargetBridge
    {
        internal static void Attach(GameItem scanner)
        {
            scanner.mayItemTargetItemFunc = Allow(scanner.mayItemTargetItemFunc);
            scanner.mayThisTargetItemFunc = Allow(scanner.mayThisTargetItemFunc);
            scanner.canItemTargetItemFunc = Allow(scanner.canItemTargetItemFunc);
            scanner.canThisTargetItemFunc = Allow(scanner.canThisTargetItemFunc);
            scanner.onItemTargetItemFunc = OpenFlowScanner(scanner.onItemTargetItemFunc);
        }

        /// <summary>
        /// 保留原委托；工厂里 AquaScan 只设 mayItem/canItem，ChemScanner 只设
        /// mayThis/canThis，所以另一个槽位必然是 null，必须显式判空。
        /// </summary>
        private static Func<GameItem, GameItem, bool> Allow(
            Func<GameItem, GameItem, bool> original)
        {
            return (source, target) =>
                (original != null && original(source, target))
                || target.IsTag(FlowTags.DeviceContainerTag);
        }

        /// <summary>
        /// 两个原版扫描仪都在 onItemTargetItemFunc 上打开面板；装置目标改开
        /// ChemScanner 面板，避免 WaterScanner 读取不存在的 WaterFeature。
        /// </summary>
        private static Action<GameItem, GameItem> OpenFlowScanner(
            Action<GameItem, GameItem> original)
        {
            return (source, target) =>
            {
                if (target.IsTag(FlowTags.DeviceContainerTag))
                {
                    ChemScannerUIManager.Instance.Open(source, target);
                    AudioManager.Instance.Play3("scanner_activate");
                    return;
                }

                original(source, target);
            };
        }
    }

    [HarmonyPatch(typeof(AmenitiesItemDirectory), "AquaScan")]
    internal static class AquaScanFlowTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __result)
        {
            FlowScannerTargetBridge.Attach(__result);
        }
    }

    [HarmonyPatch(typeof(MedsItemDirectory), "ChemScanner")]
    internal static class ChemScannerFlowTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __result)
        {
            FlowScannerTargetBridge.Attach(__result);
        }
    }
}
