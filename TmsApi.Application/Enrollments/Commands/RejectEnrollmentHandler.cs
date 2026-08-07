using MediatR;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Commands;

public class RejectEnrollmentHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<RejectEnrollmentCommand>
{
    public async Task Handle(RejectEnrollmentCommand command, CancellationToken ct)
    {
        await enrollmentService.RejectAsync(command.EnrollmentId, ct);
    }
}
