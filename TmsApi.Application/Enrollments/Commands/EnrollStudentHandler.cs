using MediatR;
using TmsApi.Application.Common;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Enrollments.Commands;

public class EnrollStudentHandler(
    IEnrollmentService enrollmentService,
    ICourseService courseService
) : IRequestHandler<EnrollStudentCommand, Result<EnrollmentCreated, EnrollmentError>>
{
    public async Task<Result<EnrollmentCreated, EnrollmentError>> Handle(
        EnrollStudentCommand command,
        CancellationToken ct
    )
    {
        var eligibility = await enrollmentService.GetStudentEligibilityAsync(command.StudentId, ct);

        if (eligibility == StudentEnrollmentEligibility.NotFound)
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.StudentNotFound(command.StudentId)
            );

        if (eligibility == StudentEnrollmentEligibility.Inactive)
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.StudentInactive(command.StudentId)
            );

        var course = await courseService.GetByCodeAsync(command.CourseCode, ct);
        if (course is null)
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.CourseNotFound(command.CourseCode)
            );
        if (course.Enrollments.Count >= course.MaxCapacity)
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.CourseFull(course.Title, course.MaxCapacity)
            );
        if (await enrollmentService.ExistsAsync(command.StudentId, command.CourseCode, ct))
            return Result<EnrollmentCreated, EnrollmentError>.Failure(
                EnrollmentError.AlreadyEnrolled(command.StudentId, command.CourseCode)
            );
        var enrollment = new Enrollment
        {
            StudentId = command.StudentId,
            CourseId = course.Id,
            EnrolledAt = DateTime.UtcNow,
        };
        try
        {
            await enrollmentService.AddAsync(enrollment, ct);
        }
        catch (EnrollmentRejectedException exception)
        {
            return Result<EnrollmentCreated, EnrollmentError>.Failure(exception.Error);
        }

        return Result<EnrollmentCreated, EnrollmentError>.Success(
            new EnrollmentCreated(enrollment.Id, enrollment.StudentId, course.Code)
        );
    }
}
