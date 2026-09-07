using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TmsApi.Application.DTOs;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Authorization;

public sealed class EnrollmentAccessFilter(TmsAccess access, TmsDbContext db) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.IsInRole("Admin")) { await next(); return; }
        if (context.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor action && action.ActionName == "GetAll")
        { context.Result = new ForbidResult(); return; }
        var ct = context.HttpContext.RequestAborted;
        var args = context.ActionArguments;
        bool allowed = false;
        if (args.TryGetValue("command", out var value) && value is EnrollStudentCommand command)
            allowed = await access.OwnsStudentAsync(user, command.StudentId, ct);
        else if (args.TryGetValue("courseId", out value) && value is int courseId)
        {
            allowed = await access.ManagesCourseAsync(user, courseId, ct);
            if (HttpMethods.IsPost(context.HttpContext.Request.Method) &&
                args.TryGetValue("request", out value) && value is EnrollStudentRequest request)
                allowed = allowed || await access.OwnsStudentAsync(user, request.StudentId, ct);
        }
        else if (args.TryGetValue("studentId", out value) && value is int studentId)
            allowed = await access.OwnsStudentAsync(user, studentId, ct);
        else if ((args.TryGetValue("id", out value) || args.TryGetValue("enrollmentId", out value)) && value is int id)
        {
            var course = await db.Enrollments.Where(e => e.Id == id).Select(e => (int?)e.CourseId).SingleOrDefaultAsync(ct);
            allowed = course.HasValue && await access.ManagesCourseAsync(user, course.Value, ct);
        }
        // Unscoped enrollment lists are administrator-only.
        if (!allowed) { context.Result = new ForbidResult(); return; }
        await next();
    }
}
