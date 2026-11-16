using System;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 让原版 AquaScan / 化学扫描仪接受流源装置容器。
    ///
    /// 只在扫描仪工厂返回时包装一次目标委托，不遍历库存、不逐帧查找扫描仪。
    /// 高亮走 <see cref="GameItem.MayTarget"/> + <see cref="GameItem.CanTarget"/>。
    ///
    /// 不能把两个目标槽位都补成非 null：GameItem 对 may/can 做三态 AND，
    /// 原本为 null 的槽位是中性；补成 false 会把原版容器的扫描判成不可用。
    /// AquaScan 只用 mayItem/canItem，ChemScanner 只用 mayThis/canThis，
    /// 所以两边分别只扩展自己实际使用的一组。
    /// </summary>
    internal static class FlowScannerTargetBridge
    {
        /// <summary>AquaScan：mayItemTargetItemFunc / canItemTargetItemFunc。</summary>
        internal static void AttachAquaScan(GameItem scanner)
        {
            scanner.mayItemTargetItemFunc = Allow(scanner.mayItemTargetItemFunc);
            scanner.canItemTargetItemFunc = Allow(scanner.canItemTargetItemFunc);
            scanner.onItemTargetItemFunc = OpenFlowScanner(scanner.onItemTargetItemFunc);
        }

        /// <summary>ChemScanner：mayThisTargetItemFunc / canThisTargetItemFunc。</summary>
        internal static void AttachChemScanner(GameItem scanner)
        {
            scanner.mayThisTargetItemFunc = Allow(scanner.mayThisTargetItemFunc);
            scanner.canThisTargetItemFunc = Allow(scanner.canThisTargetItemFunc);
            scanner.onItemTargetItemFunc = OpenFlowScanner(scanner.onItemTargetItemFunc);
        }

        /// <summary>
        /// 保留原委托；工厂只设置当前扫描仪使用的那一个槽位，另一个保持 null。
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
            FlowScannerTargetBridge.AttachAquaScan(__result);
        }
    }

    [HarmonyPatch(typeof(MedsItemDirectory), "ChemScanner")]
    internal static class ChemScannerFlowTargetPatch
    {
        [HarmonyPostfix]
        private static void Postfix(GameItem __result)
        {
            FlowScannerTargetBridge.AttachChemScanner(__result);
        }
    }
}
