using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql.NameTranslation;
using Scalar.AspNetCore;
using Tms.Api.Authorization;
using TmsApi.Api.Authorization;
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
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Workers;

// ============================================================================
// APPLICATION BUILDER
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

var npgsqlNameTranslator = new NpgsqlNullNameTranslator();

// ============================================================================
// DEPENDENCY INJECTION VALIDATION
// ============================================================================

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
{
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly);
});

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

// ============================================================================
// GLOBAL EXCEPTION HANDLING + PROBLEM DETAILS
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
// ANTIFORGERY / XSRF
// ============================================================================

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

// ============================================================================
// RATE LIMITING
// ============================================================================

builder.Services.AddRateLimiter(options =>
{
    // ------------------------------------------------------------------------
    // GLOBAL RATE LIMITER
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
// RATE LIMIT PROBLEM DETAILS RESPONSE
// ============================================================================

static async Task WriteRateLimitResponseAsync(
    OnRejectedContext context,
    CancellationToken cancellationToken
)
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

    await context.HttpContext.Response.WriteAsJsonAsync(
        problem,
        cancellationToken: cancellationToken
    );
}

// ============================================================================
// CORS
// ============================================================================

var allowedOrigins =
    builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "TmsClient",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
        }
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

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

// ============================================================================
// TRANSCRIPT CHANNEL
// ============================================================================

builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }
    )
);

// ============================================================================
// SIGNALR TRANSCRIPT NOTIFIER
// ============================================================================

builder.Services.AddSingleton<ITranscriptNotifier, SignalRTranscriptNotifier>();

// ============================================================================
// BACKGROUND WORKER
// ============================================================================

builder.Services.AddHostedService<TranscriptWorker>();

// ============================================================================
// OPENAPI
// ============================================================================

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
                nameTranslator: npgsqlNameTranslator
            )
    );

    if (builder.Environment.IsDevelopment())
    {
        options.LogTo(Console.WriteLine, LogLevel.Information).EnableSensitiveDataLogging();
    }
});

// ============================================================================
// ASP.NET CORE IDENTITY
// ============================================================================
//
// TmsDbContext now inherits:
//
// IdentityDbContext<TmsUser>
//
// This registers:
//
// UserManager<TmsUser>
// RoleManager<IdentityRole>
// SignInManager<TmsUser>
// PasswordHasher<TmsUser>
// IUserStore<TmsUser>
// IRoleStore<IdentityRole>
//
// and connects them to TmsDbContext.
//

builder
    .Services.AddIdentity<TmsUser, IdentityRole>(options =>
    {
        // --------------------------------------------------------------------
        // PASSWORD RULES
        // --------------------------------------------------------------------

        options.Password.RequiredLength = 8;

        options.Password.RequireDigit = true;

        options.Password.RequireLowercase = true;

        options.Password.RequireUppercase = true;

        options.Password.RequireNonAlphanumeric = false;

        // --------------------------------------------------------------------
        // USER RULES
        // --------------------------------------------------------------------

        options.User.RequireUniqueEmail = true;

        // --------------------------------------------------------------------
        // LOCKOUT
        // --------------------------------------------------------------------

        options.Lockout.AllowedForNewUsers = true;

        options.Lockout.MaxFailedAccessAttempts = 5;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<TmsDbContext>()
    .AddDefaultTokenProviders();

// ============================================================================
// AUTHORIZATION
// ============================================================================
//
// AddIdentity() already registers Identity authentication/cookie schemes.
//
// We still explicitly register authorization because we use:
//
// app.UseAuthorization()
//
// and potentially:
//
// [Authorize]
//

builder.Services.AddAuthorization();

// ============================================================================
// OPTIONS
// ============================================================================

builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        "CanEditCourse",
        policy => policy.Requirements.Add(new CourseInstructorRequirement())
    );
builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

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
// ERROR HANDLING
// ============================================================================

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

// ============================================================================
// STATUS CODE → PROBLEM DETAILS
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
// CORS
// ============================================================================
//
// Must run after routing and before authentication.
//

app.UseCors("TmsClient");

// ============================================================================
// RATE LIMITING
// ============================================================================

app.UseRateLimiter();

// ============================================================================
// AUTHENTICATION
// ============================================================================
//
// Identity authentication must come before authorization.
//

app.UseAuthentication();

// ============================================================================
// AUTHORIZATION
// ============================================================================

app.UseAuthorization();

// ============================================================================
// XSRF TOKEN COOKIE
// ============================================================================

app.Use(
    async (context, next) =>
    {
        if (
            context.User.Identity?.IsAuthenticated == true
            || context.Request.Cookies.ContainsKey("tms_auth")
        )
        {
            var antiforgery = context.RequestServices.GetRequiredService<IAntiforgery>();

            var tokens = antiforgery.GetAndStoreTokens(context);

            if (tokens.RequestToken is not null)
            {
                context.Response.Cookies.Append(
                    "XSRF-TOKEN",
                    tokens.RequestToken,
                    new CookieOptions
                    {
                        // Angular JavaScript must
                        // be able to read this token.
                        HttpOnly = false,

                        // HTTP is currently used in development.
                        Secure = !app.Environment.IsDevelopment(),

                        SameSite = SameSiteMode.Strict,
                    }
                );
            }
        }

        await next(context);
    }
);

// ============================================================================
// CUSTOM API VERSION DEPRECATION MIDDLEWARE
// ============================================================================

app.UseMiddleware<V1DeprecationMiddleware>();

// ============================================================================
// DEVELOPMENT OPENAPI + SCALAR
// ============================================================================

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
}

// ============================================================================
// HEALTH CHECK ENDPOINTS
// ============================================================================

app.MapHealthChecks("/health/live").DisableRateLimiting();

app.MapHealthChecks("/health/ready").DisableRateLimiting();

// ============================================================================
// SIGNALR HUB
// ============================================================================
//
// Angular:
//
// new HubConnectionBuilder()
//     .withUrl("/hubs/tms")
//
// Angular proxy:
//
// /hubs/** -> http://localhost:5150
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
