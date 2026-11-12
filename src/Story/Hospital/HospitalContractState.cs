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
        private const string NextPickupDayKey
            = "hospital.contract.next_pickup_day";
        private const string NextMaterialDayKey
            = "hospital.contract.next_material_day";
        private const string LastPickupDayKey
            = "hospital.contract.last_pickup_day";
        private const string LastMaterialDayKey
            = "hospital.contract.last_material_day";
        private const string OfferReadyKey
            = "hospital.contract.offer_ready";
        private const string OfferDeclinedKey
            = "hospital.contract.offer_declined";

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
            get { return ReadInt(NextPickupDayKey); }
        }

        internal static int NextMaterialDay
        {
            get { return ReadInt(NextMaterialDayKey); }
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

        internal static void Start(int day)
        {
            if (Started)
            {
                return;
            }

            PlayerStore.Instance.modData[StartedKey] = "1";
            PlayerStore.Instance.modData[StartDayKey] = day.ToString();
            PlayerStore.Instance.modData[NextPickupDayKey]
                = (day + 3).ToString();
            PlayerStore.Instance.modData[NextMaterialDayKey]
                = (day + 7).ToString();
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
                    NextPickupDay,
                    day);
        }

        internal static bool IsMaterialDue(int day)
        {
            return Started
                && HospitalContractSchedule.IsDue(
                    NextMaterialDay,
                    day);
        }

        internal static bool ShouldRunPickup(int day)
        {
            return HospitalContractSchedule.ShouldRunPickup(
                IsPickupDue(day),
                IsMaterialDue(day),
                LastPickupDay,
                LastMaterialDay);
        }

        internal static bool ShouldRunMaterial(int day)
        {
            return HospitalContractSchedule.ShouldRunMaterial(
                IsPickupDue(day),
                IsMaterialDue(day),
                LastPickupDay,
                LastMaterialDay);
        }

        internal static void MarkPickup(int day)
        {
            PlayerStore.Instance.modData[LastPickupDayKey]
                = day.ToString();
            PlayerStore.Instance.modData[NextPickupDayKey]
                = HospitalContractSchedule.Advance(
                    NextPickupDay,
                    day).ToString();
        }

        internal static void MarkMaterial(int day)
        {
            PlayerStore.Instance.modData[LastMaterialDayKey]
                = day.ToString();
            PlayerStore.Instance.modData[NextMaterialDayKey]
                = HospitalContractSchedule.Advance(
                    NextMaterialDay,
                    day).ToString();
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
