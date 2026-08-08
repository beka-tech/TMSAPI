using TmsApi.Domain.Enums;

namespace TmsApi.Application.DTOs;

public record EnrollmentListDto(
    string Id,
    int StudentId,
    string StudentName,
    int CourseId,
    string CourseName,
    EnrollmentStatus Status,
    DateTime EnrolledAt
);
