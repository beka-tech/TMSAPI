using MediatR;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Queries;

public class GetAllEnrollmentsQueryHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<GetAllEnrollmentsQuery, List<EnrollmentListDto>>
{
    public async Task<List<EnrollmentListDto>> Handle(
        GetAllEnrollmentsQuery request,
        CancellationToken ct
    )
    {
        var enrollments = await enrollmentService.GetAllAsync(ct, request.Page, request.PageSize, request.StudentId, request.CourseId, request.Status);

        return enrollments
            .Select(e => new EnrollmentListDto(
                e.Id.ToString(),
                e.StudentId,
                e.StudentName,
                e.CourseId,
                e.CourseTitle,
                e.Status,
                e.EnrolledAt
            ))
            .ToList();
    }
}
