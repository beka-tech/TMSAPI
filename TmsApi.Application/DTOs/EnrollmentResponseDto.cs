// namespace TMSAPI.Dtos;

namespace TmsApi.Application.DTOs;

public record EnrollmentResponseDto(int Id, int CourseId, int StudentId, DateTime EnrolledAt);
