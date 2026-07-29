using TmsApi.Application.DTOs;

namespace TmsApi.Application.Interfaces;

public interface ICachedCourseService
{
    Task<CourseResponseDto> GetCourseAsync(string code, CancellationToken ct);
    Task<List<CourseResponseDto>> GetAllCoursesAsync(CancellationToken ct);
    Task InvalidateCourseCacheAsync(CancellationToken ct);
    // Task<List<CourseResponseDto>> GetAllAsync(CancellationToken ct);
}
