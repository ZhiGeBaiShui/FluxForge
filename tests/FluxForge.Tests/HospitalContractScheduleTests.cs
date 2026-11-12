using FluxForge.Story.Hospital;
using Xunit;

namespace FluxForge.Tests
{
    public class HospitalContractScheduleTests
    {
        [Theory]
        [InlineData(107, 106, false)]
        [InlineData(107, 107, true)]
        [InlineData(107, 108, true)]
        public void IsDueComparesTodayWithNextDay(
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

        [Theory]
        [InlineData(true, false, 3, 7, true)]
        [InlineData(true, true, 3, 7, true)]
        [InlineData(true, true, 10, 7, false)]
        [InlineData(false, true, 3, 7, false)]
        public void PickupRunsWhenItIsOlderOrMaterialIsNotDue(
            bool pickupDue,
            bool materialDue,
            int lastPickup,
            int lastMaterial,
            bool expected)
        {
            Assert.Equal(
                expected,
                HospitalContractSchedule.ShouldRunPickup(
                    pickupDue,
                    materialDue,
                    lastPickup,
                    lastMaterial));
        }

        [Theory]
        [InlineData(true, true, 10, 7, true)]
        [InlineData(true, true, 3, 7, false)]
        [InlineData(true, false, 10, 7, false)]
        [InlineData(false, true, 10, 7, true)]
        public void MaterialRunsWhenItIsOlderOrPickupIsNotDue(
            bool pickupDue,
            bool materialDue,
            int lastPickup,
            int lastMaterial,
            bool expected)
        {
            Assert.Equal(
                expected,
                HospitalContractSchedule.ShouldRunMaterial(
                    pickupDue,
                    materialDue,
                    lastPickup,
                    lastMaterial));
        }
    }
}
