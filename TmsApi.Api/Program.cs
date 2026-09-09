using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using System.Threading.RateLimiting;
using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.Tokens;
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
using TmsApi.Application.Hubs;
using TmsApi.Application.Interfaces;
using TmsApi.Application.Transcripts;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using TmsApi.Infrastructure.Transcripts;
using TmsApi.Infrastructure.Workers;

var builder = WebApplication.CreateBuilder(args);

// Validate dependency injection registrations.
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Database and caching.
var npgsqlNameTranslator = new NpgsqlNullNameTranslator();

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

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };
});

// Application services and validation.
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly);
});

builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Transcripts and real-time notifications.
builder.Services.AddSignalR();

builder.Services.AddSingleton<ITranscriptStatusStore, InMemoryTranscriptStatusStore>();

builder.Services.AddSingleton(
    Channel.CreateBounded<TranscriptRequest>(
        new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.Wait }
    )
);

builder.Services.AddSingleton<ITranscriptNotifier, SignalRTranscriptNotifier>();
builder.Services.AddSingleton<IEnrollmentStatusNotifier, SignalREnrollmentStatusNotifier>();

builder.Services.AddHostedService<TranscriptWorker>();

// Authentication and authorization.
builder
    .Services.AddIdentity<TmsUser, IdentityRole>(options =>
    {
        // Password requirements.
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;

        // User requirements.
        options.User.RequireUniqueEmail = true;

        // Lockout.
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<TmsDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<TokenService>();

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            ),
        };
    });

builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        "CanEditCourse",
        policy => policy.Requirements.Add(new CourseInstructorRequirement())
    );
builder.Services.AddSingleton<IAuthorizationHandler, CourseInstructorHandler>();

// Controllers, error responses, and API documentation.
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder
    .Services.AddControllers(options =>
    {
        options.Filters.Add<AuditLogFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

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

builder.Services.AddHealthChecks();

// Antiforgery and CORS.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
});

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

// Rate limiting.
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter(
        "AuthLimiter",
        options =>
        {
            options.PermitLimit = 5;
            options.Window = TimeSpan.FromMinutes(1);
            options.QueueLimit = 0;
        }
    );

    // Global limits by API key tier.
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

    // Transcript concurrency.
    options.AddConcurrencyLimiter(
        policyName: "transcripts",
        options =>
        {
            options.PermitLimit = 5;
            options.QueueLimit = 20;
            options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        }
    );

    // Rejection response.
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, ct) => new ValueTask(WriteRateLimitResponseAsync(context, ct));
});

// Request pipeline. Middleware order is significant.
var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

// Security response headers.
app.Use(
    async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        var contentSecurityPolicy =
            app.Environment.IsDevelopment() && context.Request.Path.StartsWithSegments("/scalar")
                ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline';"
                : "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';";

        context.Response.Headers.Append("Content-Security-Policy", contentSecurityPolicy);

        await next(context);
    }
);

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRouting();
app.UseCors("TmsClient");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Issue the XSRF token cookie for authenticated clients.
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
                        // Angular must be able to read this token.
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

app.UseMiddleware<V1DeprecationMiddleware>();

// Endpoints.
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

app.MapHealthChecks("/health/live").DisableRateLimiting();
app.MapHealthChecks("/health/ready").DisableRateLimiting();
app.MapHub<TmsHub>("/hubs/tms");
app.MapControllers();

app.Run();

// Format rate-limit rejections as problem details.
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
