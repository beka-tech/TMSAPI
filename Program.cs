// using Microsoft.AspNetCore.Authentication;

// var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddControllers();

// // builder.Services.AddAuthentication();
// // builder.Services.AddAuthentication("Bearer")
// //     .AddJwtBearer();

// builder.Services
// .AddAuthentication("Training")
// .AddScheme<AuthenticationSchemeOptions,
// TrainingAuthHandler>("Training", null);

// builder.Services.AddSingleton<EnrollmentWorker>();
// builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// builder.Services.AddAuthorization();

// var app = builder.Build();

// app.UseMiddleware<RequestLoggingMiddleware>();

// app.UseRouting();

// app.UseAuthentication();
// app.UseAuthorization();

// // app.Run();

// app.MapGet("/api/assessments/results", () => Results.Ok(new
// {
//   courseCode = "CS-101",
//   studentId = "S-001",
//   letterGrade = "A"
// })).RequireAuthorization() ;

// app.Run();

// // test

using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// EXERCISE 2: Captive Dependency Detection
// ============================================
// Add validation to catch captive dependencies early
builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true; // Detects singleton holding scoped service
    options.ValidateOnBuild = true; // Validates at startup, not first request
});

builder.Services.AddControllers();

// Authentication setup (your existing code)
builder
    .Services.AddAuthentication("Training")
    .AddScheme<AuthenticationSchemeOptions, TrainingAuthHandler>("Training", null);

// ============================================
// EXERCISE 2: Service Registration (The fix is in EnrollmentWorker.cs)
// ============================================
// This registration pattern is correct - EnrollmentWorker will use IServiceScopeFactory
builder.Services.AddSingleton<EnrollmentWorker>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

builder.Services.AddAuthorization();

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
// ============================================
// Bind PaymentOptions with validation - app crashes at startup if config is missing
builder
    .Services.AddOptions<PaymentOptions>()
    .BindConfiguration("Payments")
    .ValidateDataAnnotations()
    .ValidateOnStart(); // CRITICAL: This makes it crash early, not at runtime

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Your existing endpoint
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

// Optional: Smoke test endpoint for Exercise 2
app.MapGet(
    "/api/enrollments/worker-smoke",
    async (EnrollmentWorker worker) =>
    {
        await worker.ProcessBatchAsync();
        return Results.Ok(new { message = "Batch processed successfully" });
    }
);

app.MapControllers();

app.Run();
