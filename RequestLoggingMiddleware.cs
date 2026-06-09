
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
        // 1. Generate Correlation ID and set the response header immediately
        string correlationId = Guid.NewGuid().ToString("N")[..8];
        context.Response.Headers["X-Correlation-Id"] = correlationId;

        // 2. Start measuring time
        var sw = Stopwatch.StartNew();

        // 3. Log the entry
        _logger.LogInformation("Start Request: {Method} {Path} [CorrelationId: {CorrelationId}]", 
            context.Request.Method, context.Request.Path, correlationId);

        // 4. Pass control to the next middleware in the pipeline
        await _next(context);

        // 5. Stop measuring and log the exit
        sw.Stop();
        _logger.LogInformation("End Request: {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]", 
            context.Response.StatusCode, sw.ElapsedMilliseconds, correlationId);
    }
}