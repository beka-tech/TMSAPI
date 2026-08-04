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

    // [HttpPost("{enrollmentId:int}/approve")]
    // [ProducesResponseType(typeof(EnrollmentResponseDto), StatusCodes.Status200OK)]
    // [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    // [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    // [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    // public async Task<ActionResult<EnrollmentResponseDto>> UpdateStatus(
    //     int enrollmentId,
    //     [FromBody] UpdateEnrollmentStatusRequest request,
    //     CancellationToken ct
    // )
    // {
    //     if (enrollmentId <= 0)
    //     {
    //         return BadRequest(
    //             new ProblemDetails
    //             {
    //                 Title = "Invalid enrollment ID",
    //                 Detail = "Enrollment ID must be greater than zero.",
    //                 Status = StatusCodes.Status400BadRequest,
    //             }
    //         );
    //     }

    //     try
    //     {
    //         var enrollment = await enrollmentService.UpdateStatusAsync(enrollmentId, request, ct);

    //         if (enrollment is null)
    //         {
    //             return NotFound(
    //                 new ProblemDetails
    //                 {
    //                     Title = "Enrollment not found",
    //                     Detail = $"Enrollment {enrollmentId} was not found.",
    //                     Status = StatusCodes.Status404NotFound,
    //                 }
    //             );
    //         }

    //         return Ok(enrollment);
    //     }
    //     catch (ArgumentException exception)
    //     {
    //         return BadRequest(
    //             new ProblemDetails
    //             {
    //                 Title = "Invalid enrollment status",
    //                 Detail = exception.Message,
    //                 Status = StatusCodes.Status400BadRequest,
    //             }
    //         );
    //     }
    //     catch (InvalidOperationException exception)
    //     {
    //         return Conflict(
    //             new ProblemDetails
    //             {
    //                 Title = "Enrollment status conflict",
    //                 Detail = exception.Message,
    //                 Status = StatusCodes.Status409Conflict,
    //             }
    //         );
    //     }
    // }

    [HttpPost("{enrollmentId:int}/approve")]
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
