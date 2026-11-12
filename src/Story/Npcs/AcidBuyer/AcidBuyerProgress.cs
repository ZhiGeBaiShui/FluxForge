namespace FluxForge.Story.AcidBuyer
{
    internal static class AcidBuyerProgress
    {
        private const string MetKey
            = "fluxforge.acidBuyer.met";

        private const string LastHighPurityDeliveryKey
            = "vera.last_high_purity_delivery";

        private const string HighPurityDialogueDoneKey
            = "vera.high_purity_dialogue_done";

        internal const int HighPurityDialogueThreshold = 100000;

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

        internal static int LastHighPurityDeliveryVolume
        {
            get
            {
                string raw;
                if (!PlayerStore.Instance.modData.TryGetValue(
                        LastHighPurityDeliveryKey,
                        out raw))
                {
                    return 0;
                }

                return int.Parse(raw);
            }
        }

        internal static bool HasHighPurityDialogueDone
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    HighPurityDialogueDoneKey);
            }
        }

        internal static bool ShouldShowHighPurityDialogue
        {
            get
            {
                return LastHighPurityDeliveryVolume >= HighPurityDialogueThreshold
                    && !PlayerStore.Instance.modData.ContainsKey(
                        HighPurityDialogueDoneKey);
            }
        }

        internal static void SetLastHighPurityDeliveryVolume(int units)
        {
            PlayerStore.Instance.modData[LastHighPurityDeliveryKey]
                = units.ToString();
        }

        internal static void MarkHighPurityDialogueDone()
        {
            PlayerStore.Instance.modData[HighPurityDialogueDoneKey] = "1";
        }
    }
}
