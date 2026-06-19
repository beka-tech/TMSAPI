using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TMSAPI.Data;
using TMSAPI.Entities;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// EXERCISE 3: Options Pattern with Startup Validation
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

builder.Services.AddOpenApi(); // Required before MapOpenApi() will work

// ============================================
// EXERCISE 6: ProblemDetails
// ============================================
builder.Services.AddProblemDetails();

// ============================================
// M4 EXERCISE 6: Register the DbContext
// ============================================

builder.Services.AddDbContext<TmsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TmsDatabase"))
);

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

app.MapGet(
    "/api/error",
    () =>
    {
        throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
    }
);

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<TmsDbContext>();

    // Applies any pending migrations and keeps migration history intact
    context.Database.Migrate();

    // Seed only if database has no students
    if (!context.Students.Any())
    {
        var students = new List<Student>
        {
            new()
            {
                RegistrationNumber = "TMS-2026-0001",
                Name = "Alice Smith",
                GPA = 3.8m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0002",
                Name = "Bob Jones",
                GPA = 2.9m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0003",
                Name = "Charlie Brown",
                GPA = 3.4m,
                IsActive = false,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0004",
                Name = "Diana Prince",
                GPA = 3.9m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0005",
                Name = "Evan Wright",
                GPA = 2.5m,
                IsActive = true,
            },
        };

        var courses = new List<Course>
        {
            new()
            {
                Code = "CS-101",
                Title = "Introduction to Computer Science",
                Capacity = 30,
            },
            new()
            {
                Code = "CS-201",
                Title = "Data Structures and Algorithms",
                Capacity = 25,
            },
            new()
            {
                Code = "MAT-101",
                Title = "Calculus I",
                Capacity = 40,
            },
        };

        context.Students.AddRange(students);
        context.Courses.AddRange(courses);

        // Important: Save first so students and courses get their database IDs
        context.SaveChanges();

        var enrollments = new List<Enrollment>
        {
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[0].Id,
                Grade = 4.0m,
            },
            new()
            {
                StudentId = students[0].Id,
                CourseId = courses[1].Id,
                Grade = 3.6m,
            },
            new()
            {
                StudentId = students[1].Id,
                CourseId = courses[0].Id,
                Grade = 2.8m,
            },
            new()
            {
                StudentId = students[3].Id,
                CourseId = courses[1].Id,
                Grade = 3.9m,
            },
        };

        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

app.Run();
