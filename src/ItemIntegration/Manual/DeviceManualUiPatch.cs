using HarmonyLib;

namespace FluxForge.Mod
{
    [HarmonyPatch(typeof(MultiPageUIManager), nameof(MultiPageUIManager.OpenUI))]
    internal static class DeviceManualClosesForVanillaManual
    {
        private static void Prefix()
        {
            DeviceManual.CloseForVanillaManual();
        }
    }

    [HarmonyPatch(typeof(MultiPageUIManager), nameof(MultiPageUIManager.CloseUI))]
    internal static class DeviceManualClosesWithVanillaManual
    {
        private static void Prefix()
        {
            DeviceManual.CloseForVanillaManual();
        }
    }
}
