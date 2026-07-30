using Asp.Versioning;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Scalar.AspNetCore;
using TmsApi.Api.Authentication;
using TmsApi.Api.ExceptionHandlers;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Options;
using TmsApi.Application.Behaviors;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
// ============================================
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

// Production-only leave commented in lab
// builder.Services.AddStackExchangeRedisCache(options =>
// {
// options.Configuration = builder.Configuration.GetConnectionString("Redis");
// options.InstanceName = "tms:";
// });
// builder.Services.AddHybridCache();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(EnrollStudentHandler).Assembly)
);
builder.Services.AddValidatorsFromAssembly(typeof(EnrollStudentValidator).Assembly);

// LoggingBehavior FIRST—it must wrap ValidationBehavior
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// builder.Services.AddControllers();
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogFilter>();
});

// Authentication setup
builder
    .Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// ============================================
// EXERCISE 2: Service Registration
// ============================================
// Singleton worker is okay because it should use IServiceScopeFactory internally.
// builder.Services.AddSingleton<EnrollmentWorker>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "AllowAngular",
        policy => policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()
    );
});

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// builder.Services.AddScoped<IEnrollmentService, >
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICachedCourseService, CachedCourseService>();

builder.Services.AddAuthorization();

builder.Services.AddOpenApi(); // Required before MapOpenApi() will work

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
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
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

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };
});

// ============================================
// EXERCISE 6: ProblemDetails
// ============================================
builder.Services.AddProblemDetails();

// ============================================
// M4 EXERCISE 6: Register the DbContext
// ============================================

// ============================================
// M5 EXERCISE 2: Enable Console SQL Logging
// ============================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options
        .UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
        .LogTo(Console.WriteLine, LogLevel.Information) // Log SQLto output window
        .EnableSensitiveDataLogging()
); // Show parameters in querylogs (dev only)

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
// ============================================
builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // app.MapScalarApiReference();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("TMS API Reference")
            .WithTheme(ScalarTheme.DeepSpace)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            // Tell Scalar to pull both documents into its sidebar dropdownoptions
            .AddDocument("v1", "API Version 1.0")
            .AddDocument("v2", "API Version 2.0");
    });

    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler();
}

// ============================================
// Middleware
// ============================================
// Put exception handling early, before endpoints.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseExceptionHandler();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<V1DeprecationMiddleware>();

app.UseCors("AllowAngular");

// ============================================
// Endpoints
// ============================================

app.MapControllers();

app.Run();
