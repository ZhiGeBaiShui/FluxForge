namespace FluxForge.Story.AcidBuyer
{
    internal static class AcidBuyerProgress
    {
        private const string MetKey
            = "fluxforge.acidBuyer.met";

        internal static bool HasMet
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(MetKey);
            }
        }

        internal static void MarkMet()
        {
            PlayerStore.Instance.modData[MetKey] = "1";
        }
    }
}
