using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using TmsApi.Application.Interfaces;

// TmsApi.Api.Hubs

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiVersion("2.0")]
[Tags("Enrollments")]
public class EnrollmentsController(IMediator mediator, IEnrollmentService enrollmentService)
    : ControllerBase
{
    // =========================================================
    // POST: api/v2/enrollments
    // =========================================================

    [HttpPost]
    [EndpointSummary("Enroll a student in a course")]
    [EndpointDescription(
        "Enrolls a student in a specified course. Enrollment is subject to course capacity, student eligibility, and duplicate enrollment rules."
    )]
    [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enroll(EnrollStudentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created =>
                CreatedAtAction(
                    nameof(GetByStudent),
                    new { version = "2.0", studentId = created.StudentId },
                    created
                ),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" or "student_not_found" => StatusCodes.Status404NotFound,

                    "already_enrolled" => StatusCodes.Status409Conflict,

                    "course_full" or "student_inactive" => StatusCodes.Status400BadRequest,

                    _ => StatusCodes.Status400BadRequest,
                };

                return Problem(
                    statusCode: status,
                    title: "Enrollment rejected",
                    detail: error.Message,
                    type: $"https://tms.local/errors/{error.Code}"
                );
            }
        );
    }

    // =========================================================
    // GET: api/v2/enrollments/student/1
    // =========================================================

    [HttpGet("student/{studentId:int}", Name = nameof(GetByStudent))]
    [EndpointSummary("Get enrollments by student ID")]
    [EndpointDescription("Retrieves all enrollment records for a specific student.")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByStudent(int studentId, CancellationToken ct)
    {
        var eligibility = await enrollmentService.GetStudentEligibilityAsync(studentId, ct);

        if (eligibility == StudentEnrollmentEligibility.NotFound)
        {
            return NotFound(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Student not found",
                    Detail = $"Student {studentId} was not found.",
                    Type = "https://tms.local/errors/student_not_found",
                }
            );
        }

        var enrollments = await enrollmentService.GetByStudentIdAsync(studentId, ct);

        return Ok(enrollments);
    }

    // =========================================================
    // GET: api/v2/enrollments/1/schedule
    // =========================================================

    [HttpGet("{studentId:int}/schedule")]
    [EndpointSummary("Get student's course schedule")]
    [EndpointDescription(
        "Retrieves the courses associated with a student, including course details, enrollment status, and grade."
    )]
    [ProducesResponseType(typeof(StudentScheduleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSchedule(int studentId, CancellationToken ct)
    {
        var schedule = await mediator.Send(new GetStudentScheduleQuery(studentId), ct);

        return Ok(schedule);
    }

    // =========================================================
    // GET: api/v2/enrollments
    // =========================================================

    [HttpGet]
    [EndpointSummary("Get all enrollments")]
    [EndpointDescription("Returns a complete list of all student enrollments across all courses.")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var enrollments = await mediator.Send(new GetAllEnrollmentsQuery(), ct);

        return Ok(enrollments);
    }

    // =========================================================
    // PATCH: api/v2/enrollments/1/status
    // =========================================================

    [HttpPatch("{id:int}/status")]
    [EndpointSummary("Update enrollment status")]
    [EndpointDescription(
        "Updates the status of an enrollment such as Pending, Approved, Rejected, Completed, or Dropped."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        int id,
        UpdateEnrollmentStatusRequest request,
        CancellationToken ct
    )
    {
        var updated = await enrollmentService.UpdateStatusAsync(id, request.Status!.Value, ct);

        return updated ? NoContent() : EnrollmentNotFound(id);
    }

    // =========================================================
    // PATCH: api/v2/enrollments/1/grade
    // =========================================================

    [HttpPatch("{id:int}/grade")]
    [EndpointSummary("Update enrollment grade")]
    [EndpointDescription("Assigns or updates the numeric grade for an enrollment.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateGrade(
        int id,
        UpdateEnrollmentGradeRequest request,
        CancellationToken ct
    )
    {
        var updated = await enrollmentService.UpdateGradeAsync(id, request.Grade!.Value, ct);

        return updated ? NoContent() : EnrollmentNotFound(id);
    }

    // =========================================================
    // POST: api/v2/enrollments/1/approve
    // =========================================================

    [HttpPost("{enrollmentId:int}/approve")]
    [EndpointSummary("Approve a pending enrollment")]
    [EndpointDescription("Approves a pending enrollment request.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(int enrollmentId, CancellationToken ct)
    {
        await mediator.Send(new ApproveEnrollmentCommand(enrollmentId), ct);

        return NoContent();
    }

    // =========================================================
    // POST: api/v2/enrollments/1/reject
    // =========================================================

    [HttpPost("{enrollmentId:int}/reject")]
    [EndpointSummary("Reject a pending enrollment")]
    [EndpointDescription("Rejects a pending enrollment request.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(int enrollmentId, CancellationToken ct)
    {
        await mediator.Send(new RejectEnrollmentCommand(enrollmentId), ct);

        return NoContent();
    }

    // =========================================================
    // Helper
    // =========================================================

    private NotFoundObjectResult EnrollmentNotFound(int enrollmentId)
    {
        return NotFound(
            new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Enrollment not found",
                Detail = $"Enrollment {enrollmentId} was not found.",
                Type = "https://tms.local/errors/enrollment_not_found",
            }
        );
    }
}
