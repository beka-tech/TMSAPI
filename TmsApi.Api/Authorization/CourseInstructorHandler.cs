using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TmsApi.Api.Authorization;
using TmsApi.Domain.Entities;

namespace Tms.Api.Authorization;

public class CourseInstructorHandler : AuthorizationHandler<CourseInstructorRequirement, Course>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CourseInstructorRequirement requirement,
        Course resource
    )
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isInstructor = context.User.IsInRole("Instructor");
        var isAdmin = context.User.IsInRole("Admin");

        // Admins can manage any course
        if (isAdmin)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // Instructors can only manage courses where InstructorId matches their User ID
        if (isInstructor && resource.InstructorId == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
