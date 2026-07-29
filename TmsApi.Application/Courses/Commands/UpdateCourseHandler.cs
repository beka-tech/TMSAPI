using MediatR;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Application.Courses.Commands;

public class UpdateCourseHandler(ICourseService service, ICachedCourseService cachedService)
    : IRequestHandler<UpdateCourseCommand, bool>
{
    public async Task<bool> Handle(UpdateCourseCommand command, CancellationToken ct)
    {
        var course = await service.GetByCodeAsync(command.Code, ct);

        if (course is null)
            return false;

        course.Title = command.Title;
        course.MaxCapacity = command.MaxCapacity;

        var updated = await service.UpdateAsync(course, ct);

        if (updated is null)
            return false;

        await cachedService.InvalidateCourseCacheAsync(ct);

        return true;
    }
}
