using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Hubs;
using TmsApi.Domain.Enums;

namespace TmsApi.Api.Notifications;

public class SignalREnrollmentStatusNotifier(IHubContext<TmsHub, ITmsHubClient> hubContext)
    : IEnrollmentStatusNotifier
{
    public Task EnrollmentStatusUpdatedAsync(
        int enrollmentId,
        EnrollmentStatus status,
        CancellationToken ct = default
    )
    {
        return hubContext.Clients.All.ReceiveEnrollmentStatusUpdated(
            enrollmentId.ToString(),
            status.ToString()
        );
    }
}
