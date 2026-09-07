using System.Security.Claims;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Filters;

public sealed class HttpAuditActor(IHttpContextAccessor accessor) : IAuditActor
{
    public string ActorId => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
}
