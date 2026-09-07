using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TmsApi.Api.Filters;

public class AuditLogFilter(ILogger<AuditLogFilter> logger) : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        try { await next(); }
        finally
        {
            logger.LogInformation("API {Method} {Path} returned {StatusCode} for {ActorId} (trace {TraceId})",
                context.HttpContext.Request.Method, context.HttpContext.Request.Path,
                context.HttpContext.Response.StatusCode,
                context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                context.HttpContext.TraceIdentifier);
        }
    }
}
