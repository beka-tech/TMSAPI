using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using TmsApi.Api.Authentication;
using TmsApi.Api.Filters;
using TmsApi.Api.Middlewares;
using TmsApi.Api.Options;
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

builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();

// builder.Services.AddScoped<IEnrollmentService, >
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();

builder.Services.AddAuthorization();

builder.Services.AddOpenApi(); // Required before MapOpenApi() will work

// ============================================
// EXERCISE 6: ProblemDetails
// ============================================
builder.Services.AddProblemDetails();

// ============================================
// M4 EXERCISE 6: Register the DbContext
// ============================================

// builder.Services.AddDbContext<TmsDbContext>(options =>
//     options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
// );

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
    app.MapScalarApiReference();

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

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();
    await DataSeeder.SeedAsync(context);
}

app.Run();
