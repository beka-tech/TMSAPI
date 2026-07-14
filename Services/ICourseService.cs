// using TMSAPI.Dtos;
// using TMSAPI.Entities;

// namespace TMSAPI.Services;

// public interface ICourseService
// {
//     Task<Course?> GetByIdAsync(int id, CancellationToken ct);
//     Task<Course> CreateAsync(CreateCourseRequest request, CancellationToken ct);
// }

using TMSAPI.Dtos;

namespace TMSAPI.Services;

public interface ICourseService
{
    Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct);
    Task<CourseResponseDto> CreateAsync(CreateCourseRequest request, CancellationToken ct);
}
