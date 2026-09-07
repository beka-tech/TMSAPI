using TmsApi.Domain;
using TmsApi.Domain.Enums;

namespace TmsApi.Tests.Enrollments;

public class EnrollmentRulesTests
{
    [Theory]
    [InlineData(EnrollmentStatus.Pending, true)]
    [InlineData(EnrollmentStatus.Approved, true)]
    [InlineData(EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Rejected, false)]
    public void SeatReservation_DependsOnEnrollmentStatus(EnrollmentStatus status, bool expected)
    {
        Assert.Equal(expected, EnrollmentRules.OccupiesSeat(status));
    }

    [Theory]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Approved, true)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Rejected, true)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Approved, false)]
    [InlineData(EnrollmentStatus.Rejected, EnrollmentStatus.Approved, false)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Pending, false)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Approved, false)]
    public void StatusTransition_EnforcesEnrollmentLifecycle(EnrollmentStatus from, EnrollmentStatus to, bool expected)
    {
        Assert.Equal(expected, EnrollmentRules.CanTransition(from, to));
    }
}
