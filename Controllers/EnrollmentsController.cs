using Microsoft.AspNetCore.Mvc;
using TMSAPI.Data;

[ApiController]
[Route("api/enrollments")]
public class EnrollmentsController(IEnrollmentService enrollmentService) : ControllerBase
{
    // GET /api/enrollments returns all enrollment records
    [HttpGet]
    public async Task<IActionResult> GetAll(int pageSize = 10, int pageNumber = 1)
    {
        var enrollments = await enrollmentService.GetAllAsync(pageSize, pageNumber);
        return Ok(enrollments);
    }

    // GET /api/enrollments/{id} returns one or 404
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var record = await enrollmentService.GetByIdAsync(id);
        return record is not null ? Ok(record) : NotFound();
    }

    // [HttpPost]
    // public async Task<IActionResult> Create([FromBody] CreateEnrollmentRequest request)
    // {
    //     var record = await enrollmentService.EnrollAsync(request.StudentId, request.CourseCode);
    //     return CreatedAtAction(nameof(GetById), new { id = record.Id }, record);
    // }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var deleted = await enrollmentService.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }
}
