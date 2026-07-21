// using TMSAPI.Dtos;
// using TMSAPI.Entities;

// namespace TMSAPI.Services;

// public interface ICourseService
// {
//     Task<Course?> GetByIdAsync(int id, CancellationToken ct);
//     Task<Course> CreateAsync(CreateCourseRequest request, CancellationToken ct);
// }

using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct);
    Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    );
}
