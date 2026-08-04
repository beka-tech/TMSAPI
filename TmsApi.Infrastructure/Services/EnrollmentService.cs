using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
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
                e.EnrolledAt,
                e.Status
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
                e.EnrolledAt,
                e.Status
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
                e.EnrolledAt,
                e.Status
            ))
            .ToListAsync(ct);
    }

    // public async Task<EnrollmentResponseDto> CreateAsync(
    //     int courseId,
    //     EnrollStudentRequest request,
    //     CancellationToken ct
    // )
    // {
    //     var enrollment = new Enrollment
    //     {
    //         CourseId = courseId,
    //         StudentId = request.StudentId,
    //         EnrolledAt = DateTime.UtcNow,
    //     };

    //     context.Enrollments.Add(enrollment);
    //     await context.SaveChangesAsync(ct);

    //     logger.LogInformation(
    //         "Enrollment {EnrollmentId} created for student {StudentId} in course {CourseId}",
    //         enrollment.Id,
    //         enrollment.Student,
    //         enrollment.CourseId
    //     );

    //     return await GetByIdAsync(courseId, enrollment.Id, ct)
    //         ?? throw new InvalidOperationException(
    //             $"Enrollment {enrollment.Id} was created but could not be retrieved."
    //         );
    // }

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
            Status = EnrollmentStatus.Pending,
        };

        context.Enrollments.Add(enrollment);
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Enrollment {EnrollmentId} created for student {StudentId} "
                + "in course {CourseId} with status {Status}",
            enrollment.Id,
            enrollment.StudentId,
            enrollment.CourseId,
            enrollment.Status
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
                e.EnrolledAt,
                e.Status
            ))
            .ToListAsync(ct);
    }

    // public async Task<EnrollmentResponseDto?> UpdateStatusAsync(
    //     int enrollmentId,
    //     UpdateEnrollmentStatusRequest request,
    //     CancellationToken ct
    // )
    // {
    //     var enrollment = await context
    //         .Enrollments.Include(e => e.Student)
    //         .Include(e => e.Course)
    //         .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);

    //     if (enrollment is null)
    //     {
    //         return null;
    //     }

    //     if (request.Status == EnrollmentStatus.Pending)
    //     {
    //         throw new ArgumentException("The new status must be Approved or Rejected.");
    //     }

    //     if (enrollment.Status != EnrollmentStatus.Pending)
    //     {
    //         throw new InvalidOperationException($"Enrollment is already {enrollment.Status}.");
    //     }

    //     enrollment.Status = request.Status;

    //     await context.SaveChangesAsync(ct);

    //     return new EnrollmentResponseDto(
    //         enrollment.Id,
    //         enrollment.StudentId,
    //         enrollment.Student.Name,
    //         enrollment.Student.RegistrationNumber,
    //         enrollment.CourseId,
    //         enrollment.Course.Title,
    //         enrollment.Course.Code,
    //         enrollment.Grade,
    //         enrollment.EnrolledAt,
    //         enrollment.Status
    //     );
    // }

    public async Task<EnrollmentResponseDto?> UpdateStatusAsync(
        int enrollmentId,
        UpdateEnrollmentStatusRequest request,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(request.Status))
        {
            throw new ArgumentException($"Invalid enrollment status: {request.Status}.");
        }

        if (request.Status == EnrollmentStatus.Pending)
        {
            throw new ArgumentException("The new status must be Approved or Rejected.");
        }

        var enrollment = await context
            .Enrollments.Include(e => e.Student)
            .Include(e => e.Course)
            .FirstOrDefaultAsync(e => e.Id == enrollmentId, ct);

        if (enrollment is null)
        {
            return null;
        }

        if (enrollment.Status != EnrollmentStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Enrollment {enrollmentId} cannot be changed because its current status is {enrollment.Status}."
            );
        }

        enrollment.Status = request.Status;

        await context.SaveChangesAsync(ct);

        return new EnrollmentResponseDto(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.Student.Name,
            enrollment.Student.RegistrationNumber,
            enrollment.CourseId,
            enrollment.Course.Title,
            enrollment.Course.Code,
            enrollment.Grade,
            enrollment.EnrolledAt,
            enrollment.Status
        );
    }
}
