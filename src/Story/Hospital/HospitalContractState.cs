using FluxForge.Mod;

namespace FluxForge.Story.Hospital
{
    /// <summary>医院合同状态；只存 modData，不新增存档类。</summary>
    internal static class HospitalContractState
    {
        private const string StartedKey
            = "hospital.contract.started";
        private const string StartDayKey
            = "hospital.contract.start_day";
        private const string LastPickupDayKey
            = "hospital.contract.last_pickup_day";
        private const string LastMaterialDayKey
            = "hospital.contract.last_material_day";
        private const string OfferReadyKey
            = "hospital.contract.offer_ready";
        private const string OfferDeclinedKey
            = "hospital.contract.offer_declined";
        private const string DetailsExplainedKey
            = "hospital.contract.details_explained";
        private const string TerminatedKey
            = "hospital.contract.terminated";

        internal static bool Started
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(StartedKey);
            }
        }

        internal static int StartDay
        {
            get { return ReadInt(StartDayKey); }
        }

        internal static int NextPickupDay
        {
            get { return HospitalContractSchedule.Advance(StartDay + 7, LastPickupDay); }
        }

        internal static int NextMaterialDay
        {
            get { return HospitalContractSchedule.Advance(StartDay + 3, LastMaterialDay); }
        }

        internal static int LastPickupDay
        {
            get { return ReadInt(LastPickupDayKey); }
        }

        internal static int LastMaterialDay
        {
            get { return ReadInt(LastMaterialDayKey); }
        }

        internal static bool OfferReady
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    OfferReadyKey);
            }
        }

        internal static bool OfferDeclined
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    OfferDeclinedKey);
            }
        }

        internal static bool DetailsExplained
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(DetailsExplainedKey);
            }
        }

        internal static void MarkDetailsExplained()
        {
            PlayerStore.Instance.modData[DetailsExplainedKey] = "1";
        }

        internal static bool Terminated
        {
            get
            {
                return PlayerStore.Instance.modData.ContainsKey(
                    TerminatedKey);
            }
        }

        internal static void Start(int day)
        {
            if (Started)
            {
                return;
            }

            PlayerStore.Instance.modData[StartedKey] = "1";
            PlayerStore.Instance.modData[StartDayKey] = day.ToString();
            PlayerStore.Instance.modData[LastPickupDayKey]
                = day.ToString();
            PlayerStore.Instance.modData[LastMaterialDayKey]
                = day.ToString();
            PlayerStore.Instance.modData.Remove(OfferReadyKey);
        }

        internal static bool IsPickupDue(int day)
        {
            return Started
                && HospitalContractSchedule.IsDue(
                    StartDay + 7,
                    day)
                && day > LastPickupDay;
        }

        internal static bool IsMaterialDue(int day)
        {
            return Started
                && HospitalContractSchedule.IsDue(
                    StartDay + 3,
                    day)
                && day > LastMaterialDay;
        }

        internal static bool ShouldRunPickup(int day)
        {
            return !Terminated && IsPickupDue(day);
        }

        internal static bool ShouldRunMaterial(int day)
        {
            return !Terminated && IsMaterialDue(day);
        }

        internal static void MarkPickup(int day)
        {
            PlayerStore.Instance.modData[LastPickupDayKey] = day.ToString();
        }

        internal static void MarkMaterial(int day)
        {
            PlayerStore.Instance.modData[LastMaterialDayKey] = day.ToString();
        }

        internal static void QueueOffer()
        {
            PlayerStore.Instance.modData[OfferReadyKey] = "1";
        }

        internal static void DeclineOffer()
        {
            PlayerStore.Instance.modData.Remove(OfferReadyKey);
            PlayerStore.Instance.modData[OfferDeclinedKey] = "1";
        }

        internal static void Terminate()
        {
            PlayerStore.Instance.modData[TerminatedKey] = "1";
        }

        private static int ReadInt(string key)
        {
            string raw;
            if (!PlayerStore.Instance.modData.TryGetValue(key, out raw))
            {
                return 0;
            }

            return int.Parse(raw);
        }
    }
}
