using System.Globalization;

namespace FluxForge.Story.Hospital
{
    /// <summary>最高需求：玩家接受后累计已提交的三种100%纯试剂；拒绝后不再提议。</summary>
    internal static class HospitalHighestDemandState
    {
        private const string StartedKey = "hospital.highest_demand.started";
        private const string CompletedKey = "hospital.highest_demand.completed";
        private const string AcidKey = "hospital.highest_demand.acid";
        private const string BaseKey = "hospital.highest_demand.base";
        private const string AlcoholKey = "hospital.highest_demand.alcohol";
        private const double TargetMicrolitres = 1000000d;

        internal static bool Started
        {
            get { return PlayerStore.Instance.modData.ContainsKey(StartedKey); }
        }

        internal static bool Declined
        {
            get { return PlayerStore.Instance.modData.ContainsKey("hospital.highest_demand.declined"); }
        }

        internal static void Decline()
        {
            PlayerStore.Instance.modData["hospital.highest_demand.declined"] = "1";
        }

        internal static bool Completed
        {
            get { return PlayerStore.Instance.modData.ContainsKey(CompletedKey); }
        }

        internal static bool Active
        {
            get { return Started && !Completed; }
        }

        internal static double Acid { get { return Read(AcidKey); } }
        internal static double Base { get { return Read(BaseKey); } }
        internal static double Alcohol { get { return Read(AlcoholKey); } }

        internal static bool TargetMet
        {
            get
            {
                return Acid >= TargetMicrolitres
                    && Base >= TargetMicrolitres
                    && Alcohol >= TargetMicrolitres;
            }
        }

        internal static void Start()
        {
            PlayerStore.Instance.modData[StartedKey] = "1";
        }

        internal static void Record(HospitalContractInspection inspection)
        {
            Write(AcidKey, Acid + inspection.PureAcidMicrolitres);
            Write(BaseKey, Base + inspection.PureBaseMicrolitres);
            Write(AlcoholKey, Alcohol + inspection.PureAlcoholMicrolitres);
        }

        internal static void Complete()
        {
            PlayerStore.Instance.modData[CompletedKey] = "1";
        }

        private static double Read(string key)
        {
            string value;
            return PlayerStore.Instance.modData.TryGetValue(key, out value)
                ? double.Parse(value, CultureInfo.InvariantCulture) : 0d;
        }

        private static void Write(string key, double value)
        {
            PlayerStore.Instance.modData[key]
                = value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
