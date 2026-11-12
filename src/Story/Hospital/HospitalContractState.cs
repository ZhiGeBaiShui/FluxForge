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
        private const string ScheduleVersionKey
            = "hospital.contract.schedule_version";
        private const string CurrentScheduleVersion = "2";

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
                = (day + 7).ToString();
            PlayerStore.Instance.modData[NextMaterialDayKey]
                = (day + 3).ToString();
            PlayerStore.Instance.modData[LastPickupDayKey]
                = day.ToString();
            PlayerStore.Instance.modData[LastMaterialDayKey]
                = day.ToString();
            PlayerStore.Instance.modData[ScheduleVersionKey]
                = CurrentScheduleVersion;
            PlayerStore.Instance.modData.Remove(OfferReadyKey);
        }

        /// <summary>
        /// 旧版合同把取货放在 +3、送料放在 +7；读档时只迁移一次。
        /// </summary>
        internal static void MigrateSchedule()
        {
            if (!Started
                || PlayerStore.Instance.modData.ContainsKey(
                    ScheduleVersionKey))
            {
                return;
            }

            int start = StartDay;
            int pickup = NextPickupDay;
            int material = NextMaterialDay;
            if (start > 0 && pickup > 0 && material > 0)
            {
                int pickupOffset = Mod(pickup - start, 7);
                int materialOffset = Mod(material - start, 7);
                if (pickupOffset == 3 && materialOffset == 0)
                {
                    PlayerStore.Instance.modData[NextPickupDayKey]
                        = material.ToString();
                    PlayerStore.Instance.modData[NextMaterialDayKey]
                        = pickup.ToString();
                }
            }

            PlayerStore.Instance.modData[ScheduleVersionKey]
                = CurrentScheduleVersion;
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

        private static int Mod(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
