using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql.NameTranslation;
using Scalar.AspNetCore;
using TmsApi.Api.Authentication;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Options;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Fail fast at startup if any registered service has a bad/incomplete
// dependency graph, instead of discovering it at runtime.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// -----------------------------------------------------------------------
// CQRS pipeline: MediatR handlers + FluentValidation validators, wired
// together through pipeline behaviors that run on every request.
// Order matters here: LoggingBehavior must run first so it wraps (logs
// the start/end of) ValidationBehavior as well as the actual handler.
// -----------------------------------------------------------------------
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly)
);
builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// Centralized exception handling → RFC 7807 ProblemDetails responses.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// MVC controllers. AuditLogFilter records who did what for every action.
// JSON enums are serialized as their string names instead of raw ints.
builder
    .Services.AddControllers(options =>
    {
        options.Filters.Add<AuditLogFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Custom "Training" auth scheme (see TrainingAuthHandler) used for all
// authentication/authorization checks in this API.
builder
    .Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);
builder.Services.AddAuthorization();

// -----------------------------------------------------------------------
// Rate limiting
// -----------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    // options.AddConcurrencyLimiter(
    //     "transcripts",
    //     opt =>
    //     {
    //         opt.PermitLimit = 5; // 5 in-flight transcripts maximum
    //         opt.QueueLimit = 20; // queue up to 20 more
    //         opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    //     }
    // );
    // Global limiter: applies to every request, tiered by API key type
    // (paid keys get the highest allowance, free keys less, anonymous
    // callers the least). Each tier is a token-bucket limiter that
    // refills over time rather than a hard fixed window.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var (partitionKey, tier) = ApiKeyResolver.Resolve(httpContext);

        return tier switch
        {
            ApiKeyTier.Paid => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"paid:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 200,
                    TokensPerPeriod = 100,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                }
            ),

            ApiKeyTier.Free => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"free:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 30,
                    TokensPerPeriod = 10,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                }
            ),

            // No recognized API key → treated as anonymous, tightest limit.
            _ => RateLimitPartition.GetTokenBucketLimiter(
                partitionKey: $"anon:{partitionKey}",
                factory: _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 5,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                }
            ),
        };
    });

    // Named policy for transcript endpoints: caps how many can run at
    // once (rather than how many per second), since these are expensive
    // long-running requests. Extra requests queue instead of failing
    // immediately, up to 20 waiting.
    options.AddConcurrencyLimiter(
        policyName: "transcripts",
        options =>
        {
            options.PermitLimit = 5;
            options.QueueLimit = 20;
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    );

    // Named policy for the search endpoint: its own token bucket, tighter
    // than the global default, with a small queue instead of none.
    options.AddTokenBucketLimiter(
        policyName: "search",
        options =>
        {
            options.TokenLimit = 10;
            options.TokensPerPeriod = 5;
            options.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
            options.QueueLimit = 2;
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            options.AutoReplenishment = true;
        }
    );

    // When any limiter above rejects a request, respond with 429 +
    // problem+json (see WriteRateLimitResponseAsync below) instead of
    // the framework's bare default response.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, ct) => new ValueTask(WriteRateLimitResponseAsync(context, ct));
});

// Builds the 429 response body/headers for a rejected request, including
// a Retry-After header taken from the limiter's own lease metadata when
// it provides one (falls back to a flat 10 seconds otherwise).
static async Task WriteRateLimitResponseAsync(OnRejectedContext context, CancellationToken ct)
{
    var retryAfter = "10";

    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var timeSpan))
    {
        retryAfter = Math.Ceiling(timeSpan.TotalSeconds).ToString();
    }

    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
    context.HttpContext.Response.Headers["Retry-After"] = retryAfter;
    context.HttpContext.Response.ContentType = "application/problem+json";

    var problem = new ProblemDetails
    {
        Title = "Rate limit exceeded",
        Detail = $"Too many requests. Retry after {retryAfter} seconds.",
        Status = StatusCodes.Status429TooManyRequests,
        Type = "https://tms.local/errors/rate_limit_exceeded",
    };

    await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: ct);
}

// Allow the local Angular dev server to call this API from the browser.
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()
    );
});

// Application/domain services.
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

// OpenAPI documents, one per API version, plus versioning itself. Clients
// can specify a version via the URL segment (e.g. /v2/...) or the
// X-Api-Version header; requests with no version specified default to v1.0.
builder.Services.AddOpenApi();
builder.Services.AddOpenApi(
    "v1",
    options =>
    {
        options.ShouldInclude = description => description.GroupName == "v1";
    }
);
builder.Services.AddOpenApi(
    "v2",
    options =>
    {
        options.ShouldInclude = description => description.GroupName == "v2";
    }
);

builder
    .Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = ApiVersionReader.Combine(
            new UrlSegmentApiVersionReader(),
            new HeaderApiVersionReader("X-Api-Version")
        );
    })
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// In-process + (optionally) distributed hybrid cache for read-heavy data
// such as course lookups (see ICachedCourseService).
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };
});

builder.Services.AddHealthChecks();

// EF Core / PostgreSQL. The Postgres "enrollment_status" enum type maps
// directly to the EnrollmentStatus C# enum. Verbose SQL logging (with
// parameter values) is only enabled in development, since it's noisy
// and can expose sensitive data in logs.
builder.Services.AddDbContext<TmsDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TmsDatabase"),
        npgsqlOptions =>
            npgsqlOptions.MapEnum<EnrollmentStatus>(
                enumName: "enrollment_status",
                schemaName: "public",
                nameTranslator: new NpgsqlNullNameTranslator()
            )
    );

    if (builder.Environment.IsDevelopment())
    {
        options.LogTo(Console.WriteLine, LogLevel.Information).EnableSensitiveDataLogging();
    }
});

// Strongly-typed "Payments" settings, validated against their data
// annotations as soon as the app starts (not lazily on first use).
builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

// Dev-only tooling: raw OpenAPI docs + Scalar's interactive API reference
// UI (with both v1 and v2 docs listed), plus the built-in developer
// exception page for full stack traces. In other environments, unhandled
// exceptions instead go through the GlobalExceptionHandler registered above.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("TMS API Reference")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .AddDocument("v1", "API Version 1.0")
            .AddDocument("v2", "API Version 2.0");
    });

    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

// -----------------------------------------------------------------------
// Middleware pipeline (order matters)
// -----------------------------------------------------------------------
app.UseExceptionHandler(); // catch unhandled exceptions as early as possible
app.UseStatusCodePages(); // add ProblemDetails-style bodies to bare error status codes

app.UseMiddleware<RequestLoggingMiddleware>(); // log every incoming request

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<V1DeprecationMiddleware>(); // warn/flag callers still on the v1 API

app.UseCors("AllowAngular");

// Health checks should NOT be rate limited
app.MapHealthChecks("/health/live").DisableRateLimiting();

app.MapHealthChecks("/health/ready").DisableRateLimiting();

app.MapControllers();

app.Run();
