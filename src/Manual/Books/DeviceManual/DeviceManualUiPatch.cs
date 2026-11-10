using FluxForge.Mod;
using HarmonyLib;

namespace FluxForge.Manual
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
