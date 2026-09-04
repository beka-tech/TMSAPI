namespace TmsApi.Application.DTOs;

public sealed record EnrollmentDto(
    int Id,
    int StudentId,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    decimal? Grade,
    string Status,
    DateTimeOffset EnrolledAt
);
