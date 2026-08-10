using System.Text.Json.Serialization;
using System.Threading.Channels;
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
using TmsApi.Api.Hubs;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Notifications;
using TmsApi.Api.Options;
using TmsApi.Api.RateLimiting;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Transcripts;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

// ============================================================================
// DEPENDENCY INJECTION VALIDATION
// ============================================================================

// Detect invalid DI lifetimes and missing dependencies during startup
// instead of discovering them later while the application is running.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// ============================================================================
// SIGNALR
// ============================================================================

builder.Services.AddSignalR();

// ============================================================================
// CQRS + MEDIATR
// ============================================================================

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly)
);

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ============================================================================
// GLOBAL EXCEPTION HANDLING
// ============================================================================

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddProblemDetails();

// ============================================================================
// CONTROLLERS
// ============================================================================

builder
    .Services.AddControllers(options =>
    {
        options.Filters.Add<AuditLogFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ============================================================================
// AUTHENTICATION + AUTHORIZATION
// ============================================================================

builder
    .Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

builder.Services.AddAuthorization();

// ============================================================================
// RATE LIMITING
// ============================================================================

builder.Services.AddRateLimiter(options =>
{
    // ------------------------------------------------------------------------
    // GLOBAL RATE LIMITER
    //
    // Every caller gets their own partition.
    //
    // Paid:
    //      200 token capacity
    //      +100 every 10 seconds
    //
    // Free:
    //      30 token capacity
    //      +10 every 10 seconds
    //
    // Anonymous:
    //      10 token capacity
    //      +5 every 10 seconds
    // ------------------------------------------------------------------------

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

    // ------------------------------------------------------------------------
    // TRANSCRIPT CONCURRENCY LIMITER
    //
    // Maximum:
    // 5 transcript HTTP operations running simultaneously.
    //
    // Up to 20 additional requests can wait.
    // ------------------------------------------------------------------------

    options.AddConcurrencyLimiter(
        policyName: "transcripts",
        options =>
        {
            options.PermitLimit = 5;

            options.QueueLimit = 20;

            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    );

    // ------------------------------------------------------------------------
    // COURSE SEARCH RATE LIMITER
    // ------------------------------------------------------------------------

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

    // ------------------------------------------------------------------------
    // RATE LIMIT REJECTION RESPONSE
    // ------------------------------------------------------------------------

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = (context, ct) => new ValueTask(WriteRateLimitResponseAsync(context, ct));
});

// ============================================================================
// RATE LIMIT ERROR RESPONSE
// ============================================================================

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

// ============================================================================
// CORS
// ============================================================================

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()
    );
});

// ============================================================================
// APPLICATION SERVICES
// ============================================================================

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

builder.Services.AddScoped<IStudentService, StudentService>();

builder.Services.AddScoped<ICourseService, CourseService>();

builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

// ============================================================================
// TRANSCRIPT STATUS STORE
// ============================================================================
//
// Singleton is important.
//
// The controller creates the transcript status and the BackgroundService
// later updates that same status.
//

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

// ============================================================================
// TRANSCRIPT CHANNEL
// ============================================================================
//
// Producer:
//      TranscriptsController
//
// Consumer:
//      TranscriptWorker
//
// Capacity:
//      100 jobs
//

builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }
    )
);

// ============================================================================
// SIGNALR TRANSCRIPT NOTIFIER
// ============================================================================
//
// IMPORTANT:
//
// TranscriptWorker is a BackgroundService.
//
// BackgroundService / IHostedService is effectively a Singleton.
//
// Therefore we MUST NOT constructor-inject a Scoped
// ITranscriptNotifier into it.
//
// SignalRTranscriptNotifier is stateless and uses IHubContext,
// so it can safely be registered as Singleton.
//
// Dependency flow:
//
// TranscriptWorker
//       ↓
// ITranscriptNotifier
//       ↓
// SignalRTranscriptNotifier
//       ↓
// IHubContext<TmsHub, ITmsHubClient>
//       ↓
// SignalR student group
//

builder.Services.AddSingleton<ITranscriptNotifier, SignalRTranscriptNotifier>();

// ============================================================================
// BACKGROUND WORKER
// ============================================================================

builder.Services.AddHostedService<TranscriptWorker>();

// ============================================================================
// OPENAPI
// ============================================================================

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

// ============================================================================
// API VERSIONING
// ============================================================================

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

// ============================================================================
// HYBRID CACHE
// ============================================================================

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),

        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };
});

// ============================================================================
// HEALTH CHECKS
// ============================================================================

builder.Services.AddHealthChecks();

// ============================================================================
// DATABASE
// ============================================================================

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

// ============================================================================
// OPTIONS
// ============================================================================

builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// ============================================================================
// BUILD APPLICATION
// ============================================================================

var app = builder.Build();

// ============================================================================
// DEVELOPMENT / PRODUCTION ERROR HANDLING
// ============================================================================

if (app.Environment.IsDevelopment())
{
    // ------------------------------------------------------------------------
    // OpenAPI
    // ------------------------------------------------------------------------

    app.MapOpenApi();

    // ------------------------------------------------------------------------
    // Scalar API Documentation
    // ------------------------------------------------------------------------

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

// ============================================================================
// STATUS CODE RESPONSES
// ============================================================================

app.UseStatusCodePages();

// ============================================================================
// CUSTOM REQUEST LOGGING
// ============================================================================

app.UseMiddleware<RequestLoggingMiddleware>();

// ============================================================================
// ROUTING
// ============================================================================

app.UseRouting();

// ============================================================================
// RATE LIMITING
// ============================================================================

app.UseRateLimiter();

// ============================================================================
// CORS
// ============================================================================
//
// CORS should run before authorization/endpoints so the browser
// can correctly negotiate cross-origin requests.
//

app.UseCors("AllowAngular");

// ============================================================================
// AUTHENTICATION
// ============================================================================

app.UseAuthentication();

// ============================================================================
// AUTHORIZATION
// ============================================================================

app.UseAuthorization();

// ============================================================================
// CUSTOM MIDDLEWARE
// ============================================================================

app.UseMiddleware<V1DeprecationMiddleware>();

// ============================================================================
// HEALTH CHECK ENDPOINTS
// ============================================================================
//
// Health checks must remain available even when API clients
// are being rate limited.
//

app.MapHealthChecks("/health/live").DisableRateLimiting();

app.MapHealthChecks("/health/ready").DisableRateLimiting();

// ============================================================================
// SIGNALR HUB
// ============================================================================
//
// Client connection:
//
// /hubs/tms?studentId=1
//

app.MapHub<TmsHub>("/hubs/tms");

// ============================================================================
// CONTROLLERS
// ============================================================================

app.MapControllers();

// ============================================================================
// RUN APPLICATION
// ============================================================================

app.Run();
