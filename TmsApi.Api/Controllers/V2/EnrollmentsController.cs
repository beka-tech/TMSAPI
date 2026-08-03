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
}
