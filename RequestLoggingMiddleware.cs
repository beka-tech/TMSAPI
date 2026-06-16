using System.Diagnostics;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = Guid.NewGuid().ToString("N")[..8];
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "Start Request: {Method} {Path} [CorrelationId: {CorrelationId}]",
            context.Request.Method,
            context.Request.Path,
            correlationId
        );

        await _next(context);

        sw.Stop();
        _logger.LogInformation(
            "End Request: {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            correlationId
        );
    }
}
