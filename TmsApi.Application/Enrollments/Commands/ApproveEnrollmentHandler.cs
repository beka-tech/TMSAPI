using MediatR;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Commands;

public class ApproveEnrollmentHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<ApproveEnrollmentCommand>
{
    public async Task Handle(ApproveEnrollmentCommand request, CancellationToken ct)
    {
        await enrollmentService.ApproveAsync(request.EnrollmentId, ct);
    }
}
