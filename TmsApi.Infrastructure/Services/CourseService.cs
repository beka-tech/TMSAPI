using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TmsApi.Application.DTOs;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Persistence;

// namespace TMSAPI.Services;
namespace TmsApi.Infrastructure.Services;

public class CourseService(TmsDbContext context, ILogger<CourseService> logger) : ICourseService
{
    public async Task<PagedResponse<CourseResponseDto>> GetCoursesAsync(
        PagedRequest request,
        CancellationToken ct
    )
    {
        // 1. Begin with an IQueryable.
        // Nothing has been sent to PostgreSQL yet.
        IQueryable<Course> query = context.Courses.AsNoTracking();

        // 2. Apply search before counting.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();

            query = query.Where(c =>
                EF.Functions.ILike(c.Title, $"%{search}%")
                || EF.Functions.ILike(c.Code, $"%{search}%")
            );
        }

        // 3. Count all matching courses before pagination.
        var totalCount = await query.CountAsync(ct);

        // 4. Apply a safe and deterministic sort.
        IOrderedQueryable<Course> sortedQuery = request.OrderBy.ToLowerInvariant() switch
        {
            "code" when request.Descending => query
                .OrderByDescending(c => c.Code)
                .ThenBy(c => c.Id),

            "code" => query.OrderBy(c => c.Code).ThenBy(c => c.Id),

            "maxcapacity" when request.Descending => query
                .OrderByDescending(c => c.MaxCapacity)
                .ThenBy(c => c.Id),

            "maxcapacity" => query.OrderBy(c => c.MaxCapacity).ThenBy(c => c.Id),

            "title" when request.Descending => query
                .OrderByDescending(c => c.Title)
                .ThenBy(c => c.Id),

            // Unknown values fall back to Title.
            _ => query.OrderBy(c => c.Title).ThenBy(c => c.Id),
        };

        // 5. Apply pagination and project before materialising.
        var items = await sortedQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
            ))
            .ToListAsync(ct);

        // 6. Build the response.
        return new PagedResponse<CourseResponseDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize,
        };
    }

    public Task<CourseResponseDto?> GetByIdAsync(int id, CancellationToken ct)
    {
        return context
            .Courses.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto(
                c.Id,
                c.Code,
                c.Title,
                c.MaxCapacity,
                c.Enrollments.Count
            ))
            .FirstOrDefaultAsync(ct);
    }

    public Task<Course?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return context
            .Courses.Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Code == code, ct);
    }

    public async Task<CourseResponseDto> CreateAsync(
        CreateCourseRequest request,
        CancellationToken ct
    )
    {
        var course = new Course
        {
            Code = request.Code,
            Title = request.Title,
            MaxCapacity = request.MaxCapacity,
        };

        context.Courses.Add(course);
        await context.SaveChangesAsync(ct);
        logger.LogInformation("Created course {CourseId} ({Code})", course.Id, course.Code);
        return (await GetByIdAsync(course.Id, ct))!;
    }

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) =>
        context.Courses.AsNoTracking().AnyAsync(c => c.Code == code, ct);
}
