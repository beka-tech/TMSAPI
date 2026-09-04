using TmsApi.Domain.Enums;

namespace TmsApi.Application.Hubs;

public interface IEnrollmentStatusNotifier
{
    Task EnrollmentStatusUpdatedAsync(
        int enrollmentId,
        EnrollmentStatus status,
        CancellationToken ct = default
    );
}
