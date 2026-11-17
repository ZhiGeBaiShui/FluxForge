using System;
using HarmonyLib;

namespace FluxForge.Story.Hospital
{
    /// <summary>在原版派系犯罪价值修正入口沿用“守口如瓶”的整数减半。</summary>
    [HarmonyPatch(typeof(SecData), "ApplyCrimeModifier", new Type[] { typeof(string), typeof(int) })]
    internal static class HospitalCrimeModifierPatch
    {
        internal static bool Prefix(string factionId, int value, ref int __result)
        {
            if (factionId == HospitalReputation.FactionId
                && StoreReputation.IsPerkUnlocked(
                    HospitalReputationTexts.SpecialProcurementPerkId))
            {
                __result = value / 2;
                return false;
            }

            return true;
        }
    }
}
