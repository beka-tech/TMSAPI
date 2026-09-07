using Microsoft.Extensions.Diagnostics.HealthChecks;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Health;

public sealed class DatabaseHealthCheck(TmsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            return await db.Database.CanConnectAsync(ct) ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Database unavailable.");
        }
        catch (Exception) { return HealthCheckResult.Unhealthy("Database unavailable."); }
    }
}
