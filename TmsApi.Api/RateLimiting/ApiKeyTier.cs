namespace TmsApi.Api.RateLimiting;

public enum ApiKeyTier { Anonymous, Free, Paid }

public static class ApiKeyResolver
{
    // Until a persisted API-key issuer exists, unverified headers never select a tier or partition.
    public static (string PartitionKey, ApiKeyTier Tier) Resolve(HttpContext ctx) =>
        (ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous", ApiKeyTier.Anonymous);
}
