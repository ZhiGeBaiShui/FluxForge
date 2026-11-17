namespace FluxForge.Story.AcidBuyer
{
    internal static class AcidBuyerProgress
    {
        private const string MetKey
            = "fluxforge.acidBuyer.met";

        private const string HighPurityDialogueDoneKey
            = "vera.high_purity_dialogue_done";

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

        internal static bool HasHighPurityDialogueDone
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    HighPurityDialogueDoneKey);
            }
        }

        internal static void MarkHighPurityDialogueDone()
        {
            PlayerStore.Instance.modData[HighPurityDialogueDoneKey] = "1";
        }
    }
}
