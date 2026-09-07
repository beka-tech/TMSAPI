using System.ComponentModel.DataAnnotations;
using TmsApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Infrastructure.Services;

public class EnrollmentService(
    TmsDbContext context,
    ILogger<EnrollmentService> logger,
    IEnrollmentStatusNotifier enrollmentStatusNotifier
) : IEnrollmentService
{
    public async Task<StudentEnrollmentEligibility> GetStudentEligibilityAsync(
        int studentId,
        CancellationToken ct
    )
    {
        var isActive = await context
            .Students.AsNoTracking()
            .Where(s => s.Id == studentId)
            .Select(s => (bool?)s.IsActive)
            .FirstOrDefaultAsync(ct);

        return isActive switch
        {
            null => StudentEnrollmentEligibility.NotFound,
            false => StudentEnrollmentEligibility.Inactive,
            true => StudentEnrollmentEligibility.Eligible,
        };
    }

    public Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct)
    {
        return context.Enrollments.AnyAsync(
            e => e.StudentId == studentId && e.Course.Code == courseCode,
            ct
        );
    }

    public async Task AddAsync(Enrollment enrollment, CancellationToken ct)
    {
        var course = await context.Courses.SingleOrDefaultAsync(c => c.Id == enrollment.CourseId, ct);
        if (course is null)
            throw new EnrollmentRejectedException(EnrollmentError.CourseNotFound($"course {enrollment.CourseId}"));
        await EnsureStudentCanEnrollAsync(enrollment.StudentId, ct);
        if (await context.Enrollments.AnyAsync(e => e.StudentId == enrollment.StudentId && e.CourseId == enrollment.CourseId, ct))
            throw new EnrollmentRejectedException(EnrollmentError.AlreadyEnrolled(enrollment.StudentId, course.Code));
        var seats = await context.Enrollments.CountAsync(e => e.CourseId == course.Id &&
            (e.Status == EnrollmentStatus.Pending || e.Status == EnrollmentStatus.Approved), ct);
        if (seats >= course.MaxCapacity)
            throw new EnrollmentRejectedException(EnrollmentError.CourseFull(course.Title, course.MaxCapacity));
        enrollment.Status = EnrollmentStatus.Pending;
        context.Enrollments.Add(enrollment);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsDuplicateEnrollmentViolation(exception))
        {
            throw new EnrollmentRejectedException(
                EnrollmentError.AlreadyEnrolled(
                    enrollment.StudentId,
                    $"course {enrollment.CourseId}"
                )
            );
        }
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

    public async Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    )
    {
        var enrollment = new Enrollment
        {
            CourseId = courseId, StudentId = request.StudentId,
            EnrolledAt = DateTime.UtcNow, Status = EnrollmentStatus.Pending,
        };
        await AddAsync(enrollment, ct);

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

    public async Task<bool> UpdateStatusAsync(
        int enrollmentId,
        EnrollmentStatus status,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined(status))
        {
            throw new ValidationException($"'{status}' is not a valid enrollment status.");
        }

        var enrollment = await context.Enrollments.FirstOrDefaultAsync(
            e => e.Id == enrollmentId,
            ct
        );

        if (enrollment is null)
        {
            return false;
        }

        if (!EnrollmentRules.CanTransition(enrollment.Status, status))
            throw new ResourceConflictException($"Cannot change enrollment from {enrollment.Status} to {status}.");
        enrollment.Status = status;
        await context.SaveChangesAsync(ct);
        await enrollmentStatusNotifier.EnrollmentStatusUpdatedAsync(enrollmentId, status, ct);

        logger.LogInformation(
            "Enrollment {EnrollmentId} status changed to {Status}",
            enrollmentId,
            status
        );

        return true;
    }

    public async Task<bool> UpdateGradeAsync(int enrollmentId, decimal grade, CancellationToken ct)
    {
        if (grade is < 0m or > 100m)
        {
            throw new ValidationException("Grade must be between 0 and 100.");
        }

        var enrollment = await context.Enrollments.FirstOrDefaultAsync(
            e => e.Id == enrollmentId,
            ct
        );

        if (enrollment is null)
        {
            return false;
        }

        if (enrollment.Status != EnrollmentStatus.Approved)
            throw new ResourceConflictException("Grades can only be changed while an enrollment is approved.");
        enrollment.Grade = grade;
        await context.SaveChangesAsync(ct);

        logger.LogInformation(
            "Enrollment {EnrollmentId} grade changed to {Grade}",
            enrollmentId,
            grade
        );

        return true;
    }

    public async Task ApproveAsync(int enrollmentId, CancellationToken ct)
    {
        if (!await UpdateStatusAsync(enrollmentId, EnrollmentStatus.Approved, ct))
        {
            throw new ResourceNotFoundException($"Enrollment {enrollmentId} was not found.");
        }
    }

    public async Task RejectAsync(int enrollmentId, CancellationToken ct)
    {
        if (!await UpdateStatusAsync(enrollmentId, EnrollmentStatus.Rejected, ct))
        {
            throw new ResourceNotFoundException($"Enrollment {enrollmentId} was not found.");
        }
    }

    private async Task EnsureStudentCanEnrollAsync(int studentId, CancellationToken ct)
    {
        var eligibility = await GetStudentEligibilityAsync(studentId, ct);

        var error = eligibility switch
        {
            StudentEnrollmentEligibility.NotFound => EnrollmentError.StudentNotFound(studentId),
            StudentEnrollmentEligibility.Inactive => EnrollmentError.StudentInactive(studentId),
            _ => null,
        };

        if (error is not null)
        {
            throw new EnrollmentRejectedException(error);
        }
    }

    private static bool IsDuplicateEnrollmentViolation(DbUpdateException exception) =>
        exception.InnerException
            is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: "IX_Enrollments_StudentId_CourseId",
            };
}
