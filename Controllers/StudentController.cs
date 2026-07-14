using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/students")]
public class StudentController(IStudentService studentService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(int pageSize = 10, int pageNumber = 1)
    {
        var students = await studentService.GetAllAsync(pageSize, pageNumber);
        return Ok(students);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var student = await studentService.GetByIdAsync(id);
        return student is not null ? Ok(student) : NotFound();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var isdeleted = await studentService.DeleteAsync(id);
        return isdeleted ? NoContent() : NotFound();
    }
}
