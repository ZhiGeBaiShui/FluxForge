using FluxForge.Story.Hospital;
using Xunit;

namespace FluxForge.Tests
{
    public class HospitalContractScheduleTests
    {
        [Theory]
        [InlineData(107, 106, false)]
        [InlineData(107, 107, true)]
        [InlineData(107, 108, false)]
        [InlineData(107, 114, true)]
        [InlineData(107, 115, false)]
        [InlineData(107, 128, true)]
        public void IsDueMatchesOnlyTheAgreedWeekday(
            int nextDay,
            int today,
            bool expected)
        {
            Assert.Equal(
                expected,
                HospitalContractSchedule.IsDue(nextDay, today));
        }

        [Theory]
        [InlineData(107, 106, 107)]
        [InlineData(107, 107, 114)]
        [InlineData(107, 114, 121)]
        [InlineData(107, 120, 121)]
        public void AdvanceSkipsPastPeriods(
            int nextDay,
            int today,
            int expected)
        {
            Assert.Equal(
                expected,
                HospitalContractSchedule.Advance(nextDay, today));
        }

    }
}
