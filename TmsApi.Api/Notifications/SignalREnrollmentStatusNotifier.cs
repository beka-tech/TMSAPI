using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TmsApi.Api.Hubs;
using TmsApi.Application.Hubs;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Notifications;

public class SignalREnrollmentStatusNotifier(IHubContext<TmsHub, ITmsHubClient> hubContext, TmsDbContext db)
    : IEnrollmentStatusNotifier
{
    public async Task EnrollmentStatusUpdatedAsync(int enrollmentId, EnrollmentStatus status, CancellationToken ct = default)
    {
        var enrollment = await db.Enrollments.Where(e => e.Id == enrollmentId)
            .Select(e => new { e.StudentId, e.Course.Code }).SingleOrDefaultAsync(ct);
        if (enrollment is null) return;
        await hubContext.Clients.Groups(GroupNames.Student(enrollment.StudentId.ToString()),
            GroupNames.Course(enrollment.Code), GroupNames.Administrators)
            .ReceiveEnrollmentStatusUpdated(enrollmentId.ToString(), status.ToString());
    }
}
