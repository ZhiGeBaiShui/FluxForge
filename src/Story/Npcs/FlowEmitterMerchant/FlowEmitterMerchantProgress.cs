using FluxForge.Mod;

namespace FluxForge.Story.FlowEmitterMerchant
{
    /// <summary>记录本剧情里已经看过的演示，不负责剧情是否首次。</summary>
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

        internal const string EmitterDemoKey
            = "fluxforge.flowEmitterDemo.emitterDemo";

        internal const string FilterDemoKey
            = "fluxforge.flowEmitterDemo.filterDemo";

        internal const string ColdTrapDemoKey
            = "fluxforge.flowEmitterDemo.coldTrapDemo";

        internal const string SoundHintKey
            = "fluxforge.flowEmitterDemo.soundHintSeen";

        internal static void MarkSoundHintSeen()
        {
            PlayerStore.Instance.modData[SoundHintKey] = "1";
        }

        internal static bool HasSoundHintSeen
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    SoundHintKey);
            }
        }

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

        internal static void MarkEmitterDemo()
        {
            PlayerStore.Instance.modData[EmitterDemoKey] = "1";
        }

        internal static void MarkFilterDemo()
        {
            PlayerStore.Instance.modData[FilterDemoKey] = "1";
        }

        internal static void MarkColdTrapDemo()
        {
            PlayerStore.Instance.modData[ColdTrapDemoKey] = "1";
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

        internal static bool HasEmitterDemo
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    EmitterDemoKey);
            }
        }

        internal static bool HasFilterDemo
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    FilterDemoKey);
            }
        }

        internal static bool HasColdTrapDemo
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    ColdTrapDemoKey);
            }
        }
    }
}
