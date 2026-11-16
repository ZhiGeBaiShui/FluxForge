using System;
using HarmonyLib;

namespace FluxForge.Mod
{
    /// <summary>
    /// 让原版 AquaScan / 化学扫描仪接受流源装置容器。
    ///
    /// 只在扫描仪工厂返回时追加一次委托，不遍历库存、不逐帧查找扫描仪。
    /// 高亮走 <see cref="GameItem.MayTarget"/> + <see cref="GameItem.CanTarget"/>。
    ///
    /// 使用 Delegate.Combine 保留原委托，不替换：
    ///   · AquaScan 的 mayItemTargetItemFunc 只认 LIQUID_CONTAINER_TAG；
    ///     canItemTargetItemFunc 原版恒为 true，无需扩展。
    ///   · ChemScanner 的 mayThisTargetItemFunc / canThisTargetItemFunc
    ///     都只认原版容器/化学品 tag，二者都需要追加装置 tag。
    ///   · 两个扫描仪的 onItemTargetItemFunc 都保留，只追加装置分支。
    ///
    /// 不能把未使用的目标槽位补成非 null：GameItem 对 may/can 做三态 AND，
    /// 原本为 null 的槽位是中性；补成 false 会把原版容器的扫描判成不可用。
    /// </summary>
    internal static class FlowScannerTargetBridge
    {
        private static readonly Action<GameItem, GameItem> OpenWaterScanner =
            (source, target) =>
            {
                WaterScannerUIManager.Instance.Open(source, target);
                AudioManager.Instance.Play3("scanner_activate");
            };

        private static readonly Action<GameItem, GameItem> OpenChemScanner =
            (source, target) =>
            {
                ChemScannerUIManager.Instance.Open(source, target);
                AudioManager.Instance.Play3("scanner_activate");
            };

        /// <summary>AquaScan 只缺 mayItem；canItem 原版为 true，保持不动。</summary>
        internal static void AttachAquaScan(GameItem scanner)
        {
            scanner.mayItemTargetItemFunc = AddDeviceTarget(
                scanner.mayItemTargetItemFunc);
            scanner.onItemTargetItemFunc = AddDeviceOpen(
                scanner.onItemTargetItemFunc,
                OpenWaterScanner);
        }

        /// <summary>ChemScanner 的 mayThis 和 canThis 都只认原版 tag。</summary>
        internal static void AttachChemScanner(GameItem scanner)
        {
            scanner.mayThisTargetItemFunc = AddDeviceTarget(
                scanner.mayThisTargetItemFunc);
            scanner.canThisTargetItemFunc = AddDeviceTarget(
                scanner.canThisTargetItemFunc);
            scanner.onItemTargetItemFunc = AddDeviceOpen(
                scanner.onItemTargetItemFunc,
                OpenChemScanner);
        }

        /// <summary>
        /// 追加一个只对装置容器返回 true 的目标判断。
        /// InvokeAllReduce 对多播委托做 OR，原版判断保持原样。
        /// </summary>
        private static Func<GameItem, GameItem, bool> AddDeviceTarget(
            Func<GameItem, GameItem, bool> original)
        {
            return (Func<GameItem, GameItem, bool>)Delegate.Combine(
                original,
                new Func<GameItem, GameItem, bool>(
                    (source, target) => target.IsTag(FlowTags.DeviceContainerTag)));
        }

        /// <summary>
        /// 追加装置面板分支；原回调仍在多播链里，只对原版目标生效。
        /// 每个扫描仪使用自己的面板，不能把 AquaScan 送进化学校验面板。
        /// 原版 Aqua/Chem 的 onItem 只识别 LIQUID_CONTAINER / CHEM tag，
        /// 装置容器不带这些 tag，因此不会重复打开原版面板。
        /// </summary>
        private static Action<GameItem, GameItem> AddDeviceOpen(
            Action<GameItem, GameItem> original,
            Action<GameItem, GameItem> openDevice)
        {
            return (Action<GameItem, GameItem>)Delegate.Combine(
                original,
                new Action<GameItem, GameItem>((source, target) =>
                {
                    if (!target.IsTag(FlowTags.DeviceContainerTag))
                    {
                        return;
                    }

                    openDevice(source, target);
                }));
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
