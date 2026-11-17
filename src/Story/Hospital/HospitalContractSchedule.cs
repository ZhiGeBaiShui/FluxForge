namespace FluxForge.Story.Hospital
{
    /// <summary>以首次约定日期的星期为锚点，不在其他星期补发跳过的业务。</summary>
    internal static class HospitalContractSchedule
    {
        internal static bool IsDue(int nextDay, int today)
        {
            return today >= nextDay && (today - nextDay) % 7 == 0;
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
