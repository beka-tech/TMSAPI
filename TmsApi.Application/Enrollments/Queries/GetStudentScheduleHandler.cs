//
using MediatR;
using TmsApi.Application.Interfaces;

namespace TmsApi.Application.Enrollments.Queries;

public sealed class GetStudentScheduleHandler(IEnrollmentService enrollmentService)
    : IRequestHandler<GetStudentScheduleQuery, ScheduleDto>
{
    public async Task<ScheduleDto> Handle(GetStudentScheduleQuery query, CancellationToken ct)
    {
        var enrollments = await enrollmentService.GetByStudentIdAsync(query.StudentId, ct);

        var items = enrollments
            .Select(e => new ScheduleItemDto(e.CourseCode, e.CourseTitle, "TBD"))
            .ToList();

        return new ScheduleDto(query.StudentId, items);
    }
}
