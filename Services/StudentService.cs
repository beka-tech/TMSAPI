using System.Globalization;
using Microsoft.EntityFrameworkCore;
using TMSAPI.Data;
using TMSAPI.Entities;

public interface IStudentService
{
    // Task<StudentRecord> GetByIdAsync(string id);
    Task<IReadOnlyList<Student>> GetAllAsync(int pageSize, int pageNumber);
    Task<Student?> GetByIdAsync(int id);
    Task<bool> DeleteAsync(int id);
}

public class StudentService : IStudentService
{
    private readonly TmsDbContext _context;

    private readonly ILogger<StudentService> _logger;

    public StudentService(TmsDbContext context, ILogger<StudentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // public async Task<Student> CreateStudentAsync()
    public async Task<IReadOnlyList<Student>> GetAllAsync(int pageSize, int pageNumber)
    {
        int skip = (pageNumber - 1) * pageSize;

        var students = await _context
            .Students.AsNoTracking()
            .OrderBy(s => s.GPA)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();
        _logger.LogInformation("Retrieved all {Count} students", students.Count);

        return students;
    }

    public async Task<Student?> GetByIdAsync(int id)
    {
        var student = await _context.Students.FindAsync(id);
        if (student is null)
        {
            _logger.LogWarning("Student {StudentId} not Found", id);
        }
        return student;
    }

    // public async Task<bool> UpdateStudentAsync(int id, string name, string )
    // {

    // }

    public async Task<bool> DeleteAsync(int id)
    {
        var studentD = await _context.Students.FindAsync(id);

        if (studentD is not null)
        {
            _context.Students.Remove(studentD);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Delete Student {StudentIID}", id);
        }
        else
        {
            _logger.LogWarning("Delete failed Student {StudentId} not found", id);
            return false;
        }
        return true;
    }
}

public record StudentRecord(
    int ID,
    string RegistrationNumber,
    string Name,
    decimal GPA,
    bool IsActive
);
