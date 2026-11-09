using FluxForge.Mod;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>记录本剧情的到访与库存进度，不负责具体剧情分支。</summary>
    internal static class FlowEmitterMerchantProgress
    {
        internal const string ArrivalSeenKey
            = "fluxforge.flowEmitterDemo.arrivalSeen";

        internal const string FirstStockKey
            = "fluxforge.flowEmitterDemo.firstStockPlaced";

        internal const string FirstStoryKey
            = "fluxforge.flowEmitterDemo.firstStoryDone";

        internal const string FarewellKey
            = "fluxforge.flowEmitterDemo.farewellDone";

        internal const string SecondVisitKey
            = "fluxforge.flowEmitterDemo.secondVisitDone";

        internal const string VisitCountKey
            = "fluxforge.flowEmitterDemo.visitCount";

        internal const string ThirdVisitKey
            = "fluxforge.flowEmitterDemo.thirdVisit";

        internal static void MarkArrivalSeen()
        {
            PlayerStore.Instance.modData[ArrivalSeenKey] = "1";
        }

        internal static void MarkFirstStory()
        {
            PlayerStore.Instance.modData[FirstStoryKey] = "1";
        }

        internal static void MarkFarewell()
        {
            PlayerStore.Instance.modData[FarewellKey] = "1";
        }

        internal static bool HasFarewell
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    FarewellKey);
            }
        }

        internal static void MarkFirstStock()
        {
            PlayerStore.Instance.modData[FirstStockKey] = "1";
        }

        internal static bool HasFirstStock
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    FirstStockKey);
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

        internal static bool HasThirdVisit
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    ThirdVisitKey);
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

        internal static bool HasFirstStory
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    FirstStoryKey);
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
