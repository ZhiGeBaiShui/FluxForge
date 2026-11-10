using FluxForge.Mod;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>记录本剧情的到访与库存进度，不负责具体剧情分支。</summary>
    internal static class FlowEmitterMerchantProgress
    {
        internal const string ArrivalSeenKey
            = "fluxforge.flowEmitterDemo.arrivalSeen";

        internal const string SecondVisitKey
            = "fluxforge.flowEmitterDemo.secondVisitDone";

        internal const string VisitCountKey
            = "fluxforge.flowEmitterDemo.visitCount";

        internal const string ThirdVisitKey
            = "fluxforge.flowEmitterDemo.thirdVisit";

        internal const string ManualGiftKey
            = "fluxforge.flowEmitterDemo.manualGift";

        internal static void MarkArrivalSeen()
        {
            PlayerStore.Instance.modData[ArrivalSeenKey] = "true";
        }

        internal static void MarkManualGift()
        {
            PlayerStore.Instance.modData[ManualGiftKey] = "1";
        }

        internal static bool HasManualGift
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    ManualGiftKey);
            }
        }

        internal static void MarkSecondVisit()
        {
            PlayerStore.Instance.modData[SecondVisitKey] = "1";
        }

        internal static int VisitCount
        {
            get
            {
                string raw;
                if (!PlayerStore.Instance.modData.TryGetValue(
                    VisitCountKey,
                    out raw))
                {
                    return 0;
                }

                return int.Parse(raw);
            }
        }

        internal static void IncrementVisitCount()
        {
            int count = VisitCount + 1;
            PlayerStore.Instance.modData[VisitCountKey]
                = count.ToString();

            if (count >= 3)
            {
                PlayerStore.Instance.modData[ThirdVisitKey] = "1";
            }
        }

        internal static bool HasSecondVisit
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    SecondVisitKey);
            }
        }

        internal static bool HasArrivalSeen
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    ArrivalSeenKey);
            }
        }
    }
}
