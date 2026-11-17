using System.Globalization;

namespace FluxForge.Story.Hospital
{
    /// <summary>跨次到访的提纯任务进度；本次交货结算不在此保存。</summary>
    internal static class HospitalSynaprestState
    {
        private const string StartedKey = "hospital.synaprest.started";
        private const string ClosedKey = "hospital.synaprest.closed";
        private const string CompletedKey = "hospital.synaprest.completed";
        private const string VolumeKey = "hospital.synaprest.volume";
        internal const double Target = 550000d;

        internal static bool Started { get { return PlayerStore.Instance.modData.ContainsKey(StartedKey); } }
        internal static bool Closed { get { return PlayerStore.Instance.modData.ContainsKey(ClosedKey); } }
        internal static bool Completed { get { return PlayerStore.Instance.modData.ContainsKey(CompletedKey); } }
        internal static bool Active { get { return Started && !Closed && !Completed; } }
        internal static double Volume
        {
            get
            {
                string value;
                return PlayerStore.Instance.modData.TryGetValue(VolumeKey, out value)
                    ? double.Parse(value, CultureInfo.InvariantCulture) : 0d;
            }
        }

        internal static bool CanOffer(int day)
        {
            return HospitalContractState.Started && !HospitalContractState.Terminated
                && HospitalHighestDemandState.Completed && HospitalReputation.Get() >= 100
                && !Started && !Closed
                && !HospitalContractState.IsMaterialDue(day)
                && !HospitalContractState.IsPickupDue(day);
        }

        internal static void Start() { PlayerStore.Instance.modData[StartedKey] = "1"; }
        internal static void Close() { PlayerStore.Instance.modData[ClosedKey] = "1"; }
        internal static void Record(double volume)
        {
            double total = Volume + volume;
            PlayerStore.Instance.modData[VolumeKey] = total.ToString("R", CultureInfo.InvariantCulture);
            if (total >= Target)
            {
                PlayerStore.Instance.modData[CompletedKey] = "1";
            }
        }
    }
}
