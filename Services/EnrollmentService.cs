using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using TMSAPI.Data;
using TMSAPI.Entities;

// The contract
public interface IEnrollmentService
{
    // Task<Enrollment> EnrollAsync(string studentId, string courseCode);
    Task<IReadOnlyList<Enrollment>> GetAllAsync(int pageSize, int pageNumber);

    Task<Enrollment?> GetByIdAsync(int id);

    // Task<IReadOnlyList<EnrollmentRecord>> GetAllAsync();
    Task<bool> DeleteAsync(int id);
}

// The in-memory implementation
public class EnrollmentService : IEnrollmentService
{
    private readonly TmsDbContext _context;

    private readonly Dictionary<string, EnrollmentRecord> _store = new();
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // public Task<EnrollmentRecord> EnrollAsync(string studentId, string courseCode)
    // {
    //     // Check for duplicate enrollment
    //     var existing = _store.Values.FirstOrDefault(e =>
    //         e.StudentId == studentId && e.CourseCode == courseCode
    //     );

    //     if (existing is not null)
    //     {
    //         _logger.LogWarning(
    //             "Duplicate enrollment attempt {StudentId} already in {CourseCode} (record {EnrollmentId})",
    //             studentId,
    //             courseCode,
    //             existing.Id
    //         );
    //         return Task.FromResult(existing);
    //     }

    //     var id = Guid.NewGuid().ToString("N")[..8];
    //     var record = new EnrollmentRecord(id, studentId, courseCode, DateTime.UtcNow);
    //     _store[id] = record;

    //     _logger.LogInformation(
    //         "Enrolled {StudentId} in {CourseCode} record {EnrollmentId}",
    //         studentId,
    //         courseCode,
    //         id
    //     );

    //     return Task.FromResult(record);
    // }

    public async Task<Enrollment?> GetByIdAsync(int id)
    {
        var record = await _context.Enrollments.FindAsync(id);
        if (record is null)
        {
            _logger.LogWarning("Enrollment {EnrollmentId} not found", id);
        }
        return record;
    }

    public async Task<IReadOnlyList<Enrollment>> GetAllAsync(int pageSize, int pageNumber)
    {
        int skip = (pageNumber - 1) * pageSize;

        var enrollments = await _context
            .Enrollments.AsNoTracking()
            .OrderBy(e => e.Id)
            .Skip(skip)
            .Take(pageSize)
            .ToListAsync();

        return enrollments;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // var removed = _store.Remove(id);

        var enrollmentD = await _context.Enrollments.FindAsync(id);

        if (enrollmentD is not null)
        {
            _context.Enrollments.Remove(enrollmentD);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Deleted enrollment {EnrollmentId}", id);
        }
        else
        {
            _logger.LogWarning("Delete failed enrollment {EnrollmentId} not found", id);
            return false;
        }
        return true;
    }
}

// The data shape
public class TmsDatabaseException(string message) : Exception(message);

public record EnrollmentRecord(string Id, string StudentId, string CourseCode, DateTime EnrolledAt);
