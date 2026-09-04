namespace TmsApi.Application.DTOs;

public sealed record StudentScheduleDto(
    int StudentId,
    IReadOnlyList<StudentScheduleItemDto> Courses
);

public sealed record StudentScheduleItemDto(
    int EnrollmentId,
    int CourseId,
    string CourseCode,
    string CourseTitle,
    string Status,
    decimal? Grade
);
