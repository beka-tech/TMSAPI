using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Mvc;
// using TmsApi.Application.Enrollments.Commands;
// using TmsApi.Application.Enrollments.Queries;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[Route("api/v2/enrollments")]
[Tags("Enrollments")]
// [ApiVersion(("2.0"))]
public sealed class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Get all enrollments")]
    [EndpointDescription("Returns all enrollments with student and course information.")]
    [ProducesResponseType(typeof(IReadOnlyList<EnrollmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<EnrollmentResponseDto>>> GetAllEnrollments(
        CancellationToken ct
    )
    {
        var enrollments = await enrollmentService.GetAllAsync(ct);

        return Ok(enrollments);
    }

    [HttpPost("{enrollmentId:int}/approve")]
    [EndpointSummary("Approve Enrollment")]
    [EndpointDescription("Approve Enrollments ")]
    public async Task<ActionResult<EnrollmentResponseDto>> UpdateStatus(
        int enrollmentId,
        [FromBody] UpdateEnrollmentStatusRequest request,
        CancellationToken ct
    )
    {
        try
        {
            var result = await enrollmentService.UpdateStatusAsync(enrollmentId, request, ct);

            if (result is null)
            {
                return NotFound(
                    new ProblemDetails
                    {
                        Title = "Enrollment not found",
                        Detail = $"Enrollment {enrollmentId} does not exist.",
                        Status = StatusCodes.Status404NotFound,
                    }
                );
            }

            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Title = "Invalid status",
                    Detail = ex.Message,
                    Status = StatusCodes.Status400BadRequest,
                }
            );
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(
                new ProblemDetails
                {
                    Title = "Status update rejected",
                    Detail = ex.Message,
                    Status = StatusCodes.Status409Conflict,
                }
            );
        }
    }
}
