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
    }
}
