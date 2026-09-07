using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/students")]
[Tags("Students")]
public class StudentsController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("List students with pagination")]
    [EndpointDescription("Returns a paginated list of all students. PageSize is capped at 50.")]
    [ProducesResponseType(typeof(IReadOnlyList<StudentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default
    )
    {
        var students = await studentService.GetAllAsync(pageSize, page, ct);
        return Ok(students.Select(ToResponse));
    }

    [HttpGet("{id:int}", Name = nameof(GetById))]
    [EndpointSummary("Get a student by ID")]
    [EndpointDescription("Retrieves a specific student's details using their unique identifier.")]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var student = await studentService.GetByIdAsync(id, ct);
        return student is null ? NotFound() : Ok(ToResponse(student));
    }

    [HttpPost]
    [EndpointSummary("Create a new student")]
    [EndpointDescription(
        "Creates a new student record with the provided details. Registration number must be unique."
    )]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateStudentRequest request, CancellationToken ct)
    {
        try
        {
            var student = await studentService.CreateAsync(request, ct);

            return CreatedAtAction(
                nameof(GetById),
                new { version = "2.0", id = student.Id },
                ToResponse(student)
            );
        }
        catch (DuplicateRegistrationNumberException exception)
        {
            return DuplicateRegistrationNumberConflict(exception);
        }
    }

    [HttpPut("{id:int}")]
    [EndpointSummary("Update an existing student")]
    [EndpointDescription(
        "Updates a student's information. All fields must be provided. Registration number must be unique."
    )]
    [ProducesResponseType(typeof(StudentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        int id,
        UpdateStudentRequest request,
        CancellationToken ct
    )
    {
        try
        {
            var student = await studentService.UpdateAsync(id, request, ct);
            return student is null ? NotFound() : Ok(ToResponse(student));
        }
        catch (DuplicateRegistrationNumberException exception)
        {
            return DuplicateRegistrationNumberConflict(exception);
        }
    }

    [HttpDelete("{id:int}")]
    [EndpointSummary("Delete a student")]
    [EndpointDescription(
        "Permanently removes a student record from the system using their unique identifier."
    )]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await studentService.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    private static StudentResponseDto ToResponse(Student student) =>
        new(student.Id, student.RegistrationNumber, student.Name, student.GPA, student.IsActive);

    private ConflictObjectResult DuplicateRegistrationNumberConflict(
        DuplicateRegistrationNumberException exception
    ) =>
        Conflict(
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Duplicate registration number",
                Detail = exception.Message,
                Type = "https://tms.local/errors/duplicate_registration_number",
            }
        );
}
