namespace FluxForge.Story.Hospital
{
    /// <summary>合同两段周节奏的纯计算。</summary>
    internal static class HospitalContractSchedule
    {
        internal static bool IsDue(int nextDay, int today)
        {
            return today >= nextDay;
        }

        internal static int Advance(int nextDay, int today)
        {
            if (nextDay > today)
            {
                return nextDay;
            }

            int periods = (today - nextDay) / 7 + 1;
            return nextDay + periods * 7;
        }

        internal static bool ShouldRunPickup(
            bool pickupDue,
            bool materialDue,
            int lastPickupDay,
            int lastMaterialDay)
        {
            return pickupDue
                && (!materialDue || lastPickupDay <= lastMaterialDay);
        }

        internal static bool ShouldRunMaterial(
            bool pickupDue,
            bool materialDue,
            int lastPickupDay,
            int lastMaterialDay)
        {
            return materialDue
                && (!pickupDue || lastMaterialDay < lastPickupDay);
        }
    }
}
