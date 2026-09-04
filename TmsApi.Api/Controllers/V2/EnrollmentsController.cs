using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Enrollments.Queries;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v{version:apiVersion}/enrollments")]
[ApiVersion("2.0")]
public class EnrollmentsController(
    IMediator mediator,
    ICourseService courseService,
    IEnrollmentService enrollmentService,
    IHubContext<TmsHub, ITmsHubClient> hubContext
) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Enroll(EnrollStudentCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        return result.Match<IActionResult>(
            onSuccess: created =>
                CreatedAtAction(
                    nameof(GetSchedule),
                    new { version = "2.0", studentId = created.StudentId },
                    created
                ),
            onFailure: error =>
            {
                var status = error.Code switch
                {
                    "course_not_found" or "student_not_found" => StatusCodes.Status404NotFound,

                    "course_full" or "already_enrolled" or "student_inactive" =>
                        StatusCodes.Status409Conflict,

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

    [HttpGet("{studentId}/schedule")]
    public async Task<IActionResult> GetSchedule(int studentId, CancellationToken ct)
    {
        var schedule = await mediator.Send(new GetStudentScheduleQuery(studentId), ct);

        return Ok(schedule);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var enrollments = await mediator.Send(new GetAllEnrollmentsQuery(), ct);

        return Ok(enrollments);
    }

    [HttpPost("{enrollmentId}/approve")]
    public async Task<IActionResult> Approve(int enrollmentId, CancellationToken ct)
    {
        await mediator.Send(new ApproveEnrollmentCommand(enrollmentId), ct);

        return NoContent();
    }

    [HttpPost("{enrollmentId}/reject")]
    public async Task<IActionResult> Reject(int enrollmentId, CancellationToken ct)
    {
        await mediator.Send(new RejectEnrollmentCommand(enrollmentId), ct);

        return NoContent();
    }
}
