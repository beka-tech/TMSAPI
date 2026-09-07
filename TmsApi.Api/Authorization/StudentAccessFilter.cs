using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TmsApi.Api.Authorization;

public sealed class StudentAccessFilter(TmsAccess access) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        var allowed = user.IsInRole("Admin") || (HttpMethods.IsGet(context.HttpContext.Request.Method) &&
            context.ActionArguments.TryGetValue("id", out var id) && id is int studentId &&
            await access.OwnsStudentAsync(user, studentId, context.HttpContext.RequestAborted));
        if (!allowed) { context.Result = new ForbidResult(); return; }
        await next();
    }
}
