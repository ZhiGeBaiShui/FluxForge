using HarmonyLib;

namespace FluxForge.Story.Hospital
{
    /// <summary>把自定义医院派系接入原版上层医院采购员使用的鉴别流程。</summary>
    [HarmonyPatch(typeof(StoreClient), "CompleteClientCreation")]
    internal static class HospitalClientCreationPatch
    {
        internal static void Postfix(StoreClient __instance, bool IgnoreExposeFunc)
        {
            if (!IgnoreExposeFunc && __instance.clientFaction == HospitalReputation.FactionId)
            {
                __instance.canClientExposeFeature = ClientCanExposeFunc.ULClientExposeCapacity;
            }
        }
    }
}
