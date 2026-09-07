using TmsApi.Domain.Enums;

namespace TmsApi.Domain;

public static class EnrollmentRules
{
    // Pending requests reserve seats. Terminal records retain history but release seats.
    public static bool OccupiesSeat(EnrollmentStatus status) =>
        status is EnrollmentStatus.Pending or EnrollmentStatus.Approved;

    public static bool CanTransition(EnrollmentStatus from, EnrollmentStatus to) => (from, to) switch
    {
        (EnrollmentStatus.Pending, EnrollmentStatus.Approved or EnrollmentStatus.Rejected) => true,
        (EnrollmentStatus.Approved, EnrollmentStatus.Completed) => true,
        _ => false,
    };
}
