using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class StudentService(TmsDbContext context, ILogger<StudentService> logger) : IStudentService
{
    public async Task<IReadOnlyList<Student>> GetAllAsync(
        int pageSize,
        int pageNumber,
        CancellationToken ct = default
    )
    {
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var safePageNumber = Math.Max(pageNumber, 1);
        var skip = (safePageNumber - 1) * safePageSize;

        var students = await context
            .Students.AsNoTracking()
            .OrderBy(s => s.GPA)
            .ThenBy(s => s.Id)
            .Skip(skip)
            .Take(safePageSize)
            .ToListAsync(ct);

        logger.LogInformation("Retrieved {Count} students", students.Count);
        return students;
    }

    public async Task<Student?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var student = await context
            .Students.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student is null)
        {
            logger.LogWarning("Student {StudentId} not found", id);
        }

        return student;
    }

    public async Task<Student> CreateAsync(
        CreateStudentRequest request,
        CancellationToken ct = default
    )
    {
        ValidateRequest(request);

        var registrationNumber = NormalizeRegistrationNumber(request.RegistrationNumber);
        await EnsureRegistrationNumberIsUniqueAsync(registrationNumber, null, ct);

        var student = new Student
        {
            RegistrationNumber = registrationNumber,
            Name = request.Name.Trim(),
            GPA = request.GPA,
            IsActive = request.IsActive,
        };

        context.Students.Add(student);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsRegistrationNumberViolation(exception))
        {
            throw new DuplicateRegistrationNumberException(registrationNumber);
        }

        logger.LogInformation(
            "Created student {StudentId} ({RegistrationNumber})",
            student.Id,
            student.RegistrationNumber
        );

        return student;
    }

    public async Task<Student?> UpdateAsync(
        int id,
        UpdateStudentRequest request,
        CancellationToken ct = default
    )
    {
        ValidateRequest(request);

        var student = await context.Students.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student is null)
        {
            logger.LogWarning("Update failed: student {StudentId} was not found", id);
            return null;
        }

        var registrationNumber = NormalizeRegistrationNumber(request.RegistrationNumber);
        await EnsureRegistrationNumberIsUniqueAsync(registrationNumber, id, ct);

        student.RegistrationNumber = registrationNumber;
        student.Name = request.Name.Trim();
        student.GPA = request.GPA;
        student.IsActive = request.IsActive ?? student.IsActive;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsRegistrationNumberViolation(exception))
        {
            throw new DuplicateRegistrationNumberException(registrationNumber);
        }

        logger.LogInformation(
            "Updated student {StudentId} ({RegistrationNumber})",
            student.Id,
            student.RegistrationNumber
        );

        return student;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var student = await context.Students.FirstOrDefaultAsync(s => s.Id == id, ct);

        if (student is null)
        {
            logger.LogWarning("Delete failed: student {StudentId} was not found", id);
            return false;
        }

        student.IsDeleted = true;
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Soft-deleted student {StudentId}", id);
        return true;
    }

    private async Task EnsureRegistrationNumberIsUniqueAsync(
        string registrationNumber,
        int? excludedStudentId,
        CancellationToken ct
    )
    {
        var exists = await context
            .Students.IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                s =>
                    s.RegistrationNumber.ToUpper() == registrationNumber
                    && (!excludedStudentId.HasValue || s.Id != excludedStudentId.Value),
                ct
            );

        if (exists)
        {
            throw new DuplicateRegistrationNumberException(registrationNumber);
        }
    }

    private static string NormalizeRegistrationNumber(string registrationNumber) =>
        registrationNumber.Trim().ToUpperInvariant();

    private static void ValidateRequest(object request) =>
        Validator.ValidateObject(request, new ValidationContext(request), validateAllProperties: true);

    private static bool IsRegistrationNumberViolation(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Students_RegistrationNumber",
            };
}


// using Microsoft.EntityFrameworkCore;
// using Microsoft.Extensions.Logging;
// using TmsApi.Application.Interfaces;
// using TmsApi.Domain.Entities;
// using TmsApi.Infrastructure.Persistence;

// namespace TmsApi.Infrastructure.Services;

// public interface IStudentService
// {
//     // Task<StudentRecord> GetByIdAsync(string id);
//     Task<IReadOnlyList<Student>> GetAllAsync(int pageSize, int pageNumber);
//     Task<Student?> GetByIdAsync(int id);
//     Task<bool> DeleteAsync(int id);
// }

// public class StudentService : IStudentService
// {
//     private readonly TmsDbContext _context;

//     private readonly ILogger<StudentService> _logger;

//     public StudentService(TmsDbContext context, ILogger<StudentService> logger)
//     {
//         _context = context;
//         _logger = logger;
//     }

//     // public async Task<Student> CreateStudentAsync()
//     public async Task<IReadOnlyList<Student>> GetAllAsync(int pageSize, int pageNumber)
//     {
//         int skip = (pageNumber - 1) * pageSize;

//         var students = await _context
//             .Students.AsNoTracking()
//             .OrderBy(s => s.GPA)
//             .Skip(skip)
//             .Take(pageSize)
//             .ToListAsync();
//         _logger.LogInformation("Retrieved all {Count} students", students.Count);

//         return students;
//     }

//     public async Task<Student?> GetByIdAsync(int id)
//     {
//         var student = await _context.Students.FindAsync(id);
//         if (student is null)
//         {
//             _logger.LogWarning("Student {StudentId} not Found", id);
//         }
//         return student;
//     }

//     // public async Task<bool> UpdateStudentAsync(int id, string name, string )
//     // {

//     // }

//     public async Task<bool> DeleteAsync(int id)
//     {
//         var studentD = await _context.Students.FindAsync(id);

//         if (studentD is not null)
//         {
//             _context.Students.Remove(studentD);
//             await _context.SaveChangesAsync();
//             _logger.LogInformation("Delete Student {StudentIID}", id);
//         }
//         else
//         {
//             _logger.LogWarning("Delete failed Student {StudentId} not found", id);
//             return false;
//         }
//         return true;
//     }
// }

// public record StudentRecord(
//     int ID,
//     string RegistrationNumber,
//     string Name,
//     decimal GPA,
//     bool IsActive
// );
