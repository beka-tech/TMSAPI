using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

// namespace TMSAPI.Services;
// namespace TmsApi.Application.Interfaces;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(TmsDbContext context, ILogger<EnrollmentService> logger)
    : IEnrollmentService
{
    public Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct)
    {
        return context
            .Enrollments.Include(e => e.Course)
            .AnyAsync(e => e.StudentId == studentId && e.Course.Code == courseCode, ct);
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct)
    {
        context.Enrollments.Add(enrollment);

        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct)
    {
        return await context
            .Enrollments.AsNoTracking()
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.Student.RegistrationNumber,
                e.CourseId,
                e.Course.Title, // CourseTitle
                e.Course.Code, // CourseCode
                e.Grade,
                e.EnrolledAt
            ))
            .ToListAsync(ct);
    }

    // public Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct) =>
    //     context
    //         .Enrollments.AsNoTracking()
    //         .Where(e => e.Id == id && e.CourseId == courseId)
    //         .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
    //         .FirstOrDefaultAsync(ct);
    public Task<EnrollmentResponseDto?> GetByIdAsync(
        int courseId,
        int enrollmentId,
        CancellationToken ct
    )
    {
        return context
            .Enrollments.AsNoTracking()
            .Where(e => e.Id == enrollmentId && e.CourseId == courseId)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.Student.RegistrationNumber,
                e.CourseId,
                e.Course.Title,
                e.Course.Code,
                e.Grade,
                e.EnrolledAt
            ))
            .FirstOrDefaultAsync(ct);
    }

    // public Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct) =>
    //     context
    //         .Enrollments.AsNoTracking()
    //         .Where(e => e.CourseId == courseId)
    //         .Select(e => new EnrollmentResponseDto(e.Id, e.CourseId, e.StudentId, e.EnrolledAt))
    //         .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(
        int courseId,
        CancellationToken ct
    )
    {
        return await context
            .Enrollments.AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.Student.RegistrationNumber,
                e.CourseId,
                e.Course.Title,
                e.Course.Code,
                e.Grade,
                e.EnrolledAt
            ))
            .ToListAsync(ct);
    }

    public async Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    )
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId,
            StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow,
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Enrollment {EnrollmentId} created for student {StudentId} in course {CourseId}",
            enrollment.Id,
            enrollment.Student,
            enrollment.CourseId
        );

        return await GetByIdAsync(courseId, enrollment.Id, ct)
            ?? throw new InvalidOperationException(
                $"Enrollment {enrollment.Id} was created but could not be retrieved."
            );
    }

    // public async Task<IReadOnlyList<Enrollment>> GetByStudentIdAsync(
    //     int studentId,
    //     CancellationToken ct
    // )
    // {
    //     return await context
    //         .Enrollments.Include(e => e.Course)
    //         .Where(e => e.StudentId == studentId)
    //         .ToListAsync(ct);
    // }
    public async Task<IReadOnlyList<EnrollmentResponseDto>> GetByStudentIdAsync(
        int studentId,
        CancellationToken ct
    )
    {
        return await context
            .Enrollments.AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.EnrolledAt)
            .Select(e => new EnrollmentResponseDto(
                e.Id,
                e.StudentId,
                e.Student.Name,
                e.Student.RegistrationNumber,
                e.CourseId,
                e.Course.Title,
                e.Course.Code,
                e.Grade,
                e.EnrolledAt
            ))
            .ToListAsync(ct);
    }
}
