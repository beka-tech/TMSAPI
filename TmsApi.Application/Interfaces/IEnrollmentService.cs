using TmsApi.Application.DTOs;
using TmsApi.Domain.Entities;
using TmsApi.Domain.Enums;

namespace TmsApi.Application.Interfaces;

public interface IEnrollmentService
{
    // Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int id, CancellationToken ct);
    Task<EnrollmentResponseDto> CreateAsync(
        int courseId,
        EnrollStudentRequest request,
        CancellationToken ct
    );

    // Task<bool> ExistsAsync(int studentId, int courseId, CancellationToken ct);
    Task<bool> ExistsAsync(int studentId, string courseCode, CancellationToken ct);
    Task AddAsync(Enrollment enrollment, CancellationToken ct);

    // Task<IReadOnlyList<Enrollment>> GetByStudentIdAsync(int studentId, CancellationToken ct);

    // Task<EnrollmentResponseDto?> GetByCourseAsync(int courseId, CancellationToken ct);
    Task<IReadOnlyList<EnrollmentResponseDto>> GetAllAsync(CancellationToken ct);

    Task<EnrollmentResponseDto?> GetByIdAsync(int courseId, int enrollmentId, CancellationToken ct);

    Task<IReadOnlyList<EnrollmentResponseDto>> GetByCourseAsync(int courseId, CancellationToken ct);

    Task<IReadOnlyList<EnrollmentResponseDto>> GetByStudentIdAsync(
        int studentId,
        CancellationToken ct
    );

    // Task<EnrollmentResponseDto?> UpdateStatusAsync(
    //     int courseId,
    //     int enrollmentId,
    //     UpdateEnrollmentStatusRequest request,
    //     EnrollmentStatus status,
    //     CancellationToken ct
    // );
    Task<EnrollmentResponseDto?> UpdateStatusAsync(
        int enrollmentId,
        UpdateEnrollmentStatusRequest request,
        CancellationToken ct
    );
}
