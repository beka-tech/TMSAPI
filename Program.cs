using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TMSAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// EXERCISE 2: Captive Dependency Detection
// ============================================
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.AddControllers();

// Authentication setup
builder
    .Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// ============================================
// EXERCISE 2: Service Registration
// ============================================
// Singleton worker is okay because it should use IServiceScopeFactory internally.
builder.Services.AddSingleton<EnrollmentWorker>();

builder.Services.AddSingleton<IEnrollmentService, EnrollmentService>();

builder.Services.AddAuthorization();

// ============================================
// EXERCISE 6: ProblemDetails
// ============================================
builder.Services.AddProblemDetails();

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
// ============================================

// ============================================
// EXERCISE 6: Register the DbContext
// ============================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
);

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
// ============================================
builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart();

var app = builder.Build();

// ============================================
// Middleware
// ============================================
// Put exception handling early, before endpoints.
app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ============================================
// Endpoints
// ============================================
app.MapGet(
        "/api/assessments/results",
        () =>
            Results.Ok(
                new
                {
                    courseCode = "CS-101",
                    studentId = "S-001",
                    letterGrade = "A",
                }
            )
    )
    .RequireAuthorization();

app.MapGet(
    "/api/error",
    () =>
    {
        throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
    }
);

app.MapControllers();

app.Run();
