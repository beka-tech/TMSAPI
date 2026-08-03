// namespace TMSAPI.Dtos;

namespace TmsApi.Application.DTOs;

public record EnrollmentResponseDto(
    int Id,
    int StudentId,
    string StudentName,
    string RegistrationNumber,
    int CourseId,
    string CourseTitle,
    string CourseCode,
    decimal? Grade,
    DateTime EnrolledAt
);
