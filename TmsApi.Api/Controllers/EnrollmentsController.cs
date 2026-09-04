using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/courses/{courseId:int}/enrollments")]
// [Route("api/enrollments")]
[Tags("Enrollments")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public class EnrollmentsController(
    ICourseService courseService,
    IEnrollmentService enrollmentService,
    IHubContext<TmsHub, ITmsHubClient> hubContext
) : ControllerBase
{
    [HttpGet(Name = "ListCourseEnrollments")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("List enrolments for a course")]
    [EndpointDescription("Returns all enrolments for the specified course.")]
    public async Task<IActionResult> GetEnrollments(int courseId, CancellationToken ct)
    {
        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
            return NotFound();

        var enrollments = await enrollmentService.GetByCourseAsync(courseId, ct);
        return Ok(enrollments);
    }

    [HttpGet("{id:int}", Name = nameof(GetEnrollment))]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [EndpointSummary("Get one enrolment for a course")]
    public async Task<IActionResult> GetEnrollment(int courseId, int id, CancellationToken ct)
    {
        var enrollment = await enrollmentService.GetByIdAsync(courseId, id, ct);
        return enrollment is null ? NotFound() : Ok(enrollment);
    }

    [HttpPost]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [EndpointSummary("Enrol a student in a course")]
    [EndpointDescription(
        "Returns 404 if the course does not exist, 409 if the course has reached MaxCapacity."
    )]
    public async Task<IActionResult> EnrollStudent(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    )
    {
        var eligibility = await enrollmentService.GetStudentEligibilityAsync(request.StudentId, ct);

        if (eligibility == StudentEnrollmentEligibility.NotFound)
        {
            return NotFound(
                new ProblemDetails
                {
                    Title = "Enrollment rejected",
                    Detail = $"Student {request.StudentId} was not found.",
                    Status = StatusCodes.Status404NotFound,
                    Type = "https://tms.local/errors/student_not_found",
                }
            );
        }

        if (eligibility == StudentEnrollmentEligibility.Inactive)
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Enrollment rejected",
                    Detail = $"Student {request.StudentId} is inactive and cannot enroll.",
                    Status = StatusCodes.Status409Conflict,
                    Type = "https://tms.local/errors/student_inactive",
                }
            );
        }

        var course = await courseService.GetByIdAsync(courseId, ct);

        if (course is null)
        {
            return NotFound();
        }

        if (course.EnrollmentCount >= course.MaxCapacity)
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Course is full",
                    Detail =
                        $"Course '{course.Title}' has reached its maximum capacity of {course.MaxCapacity}.",
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }

        try
        {
            var enrollment = await enrollmentService.CreateAsync(courseId, request, ct);
            return CreatedAtAction(
                nameof(GetEnrollment),
                new { courseId, id = enrollment.Id },
                enrollment
            );
        }
        catch (EnrollmentRejectedException exception)
        {
            var status = exception.Error.Code == "student_not_found"
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status409Conflict;

            return Problem(
                statusCode: status,
                title: "Enrollment rejected",
                detail: exception.Message,
                type: $"https://tms.local/errors/{exception.Error.Code}"
            );
        }
    }
}
