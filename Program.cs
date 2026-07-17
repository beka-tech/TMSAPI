using System.Collections.Generic;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using TMSAPI.Data;
using TMSAPI.Entities;
using TMSAPI.Filters;
using TMSAPI.Services;

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

// app.MapGet(
//     "/api/error",
//     () =>
//     {
//         throw new TmsDatabaseException("Simulated database failure for ProblemDetails testing");
//     }
// );

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
            new()
            {
                RegistrationNumber = "TMS-2026-0006",
                Name = "Fatima Ali",
                GPA = 3.7m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0007",
                Name = "George Miller",
                GPA = 2.6m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0008",
                Name = "Hanna Bekele",
                GPA = 3.2m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0009",
                Name = "Isaac Johnson",
                GPA = 3.5m,
                IsActive = false,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0010",
                Name = "Julia Roberts",
                GPA = 3.1m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0011",
                Name = "Khalid Ahmed",
                GPA = 2.8m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0012",
                Name = "Lily Adams",
                GPA = 3.6m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0013",
                Name = "Michael Brown",
                GPA = 2.4m,
                IsActive = false,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0014",
                Name = "Nora Wilson",
                GPA = 3.9m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0015",
                Name = "Omar Hassan",
                GPA = 3.0m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0016",
                Name = "Paula Green",
                GPA = 3.3m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0017",
                Name = "Quincy Taylor",
                GPA = 2.7m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0018",
                Name = "Rahel Tesfaye",
                GPA = 3.8m,
                IsActive = true,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0019",
                Name = "Samuel Lee",
                GPA = 3.4m,
                IsActive = false,
            },
            new()
            {
                RegistrationNumber = "TMS-2026-0020",
                Name = "Tina Morgan",
                GPA = 3.2m,
                IsActive = true,
            },
        };

        var courses = new List<Course>
        {
            new()
            {
                Code = "CS-101",
                Title = "Introduction to Computer Science",
                MaxCapacity = 30,
            },
            new()
            {
                Code = "CS-102",
                Title = "Programming Fundamentals",
                MaxCapacity = 35,
            },
            new()
            {
                Code = "CS-201",
                Title = "Data Structures and Algorithms",
                MaxCapacity = 25,
            },
            new()
            {
                Code = "CS-202",
                Title = "Object-Oriented Programming",
                MaxCapacity = 30,
            },
            new()
            {
                Code = "CS-301",
                Title = "Database Systems",
                MaxCapacity = 28,
            },
            new()
            {
                Code = "CS-302",
                Title = "Web Development",
                MaxCapacity = 32,
            },
            new()
            {
                Code = "CS-303",
                Title = "Software Engineering",
                MaxCapacity = 30,
            },
            new()
            {
                Code = "CS-304",
                Title = "Computer Networks",
                MaxCapacity = 25,
            },
            new()
            {
                Code = "CS-305",
                Title = "Operating Systems",
                MaxCapacity = 25,
            },
            new()
            {
                Code = "CS-401",
                Title = "Artificial Intelligence",
                MaxCapacity = 20,
            },
            new()
            {
                Code = "MAT-101",
                Title = "Calculus I",
                MaxCapacity = 40,
            },
            new()
            {
                Code = "MAT-102",
                Title = "Calculus II",
                MaxCapacity = 35,
            },
            new()
            {
                Code = "MAT-201",
                Title = "Linear Algebra",
                MaxCapacity = 30,
            },
            new()
            {
                Code = "STAT-101",
                Title = "Introduction to Statistics",
                MaxCapacity = 40,
            },
            new()
            {
                Code = "ENG-101",
                Title = "Academic Writing",
                MaxCapacity = 45,
            },
            new()
            {
                Code = "BUS-101",
                Title = "Introduction to Business",
                MaxCapacity = 50,
            },
            new()
            {
                Code = "PHY-101",
                Title = "General Physics",
                MaxCapacity = 35,
            },
            new()
            {
                Code = "CHEM-101",
                Title = "General Chemistry",
                MaxCapacity = 35,
            },
            new()
            {
                Code = "BIO-101",
                Title = "General Biology",
                MaxCapacity = 35,
            },
            new()
            {
                Code = "ECON-101",
                Title = "Principles of Economics",
                MaxCapacity = 45,
            },
        };

        context.Students.AddRange(students);
        context.Courses.AddRange(courses);

        // Save first so students and courses get their generated database IDs
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
                StudentId = students[1].Id,
                CourseId = courses[1].Id,
                Grade = 2.8m,
            },
            new()
            {
                StudentId = students[2].Id,
                CourseId = courses[2].Id,
                Grade = 3.4m,
            },
            new()
            {
                StudentId = students[3].Id,
                CourseId = courses[3].Id,
                Grade = 3.9m,
            },
            new()
            {
                StudentId = students[4].Id,
                CourseId = courses[4].Id,
                Grade = 2.5m,
            },
            new()
            {
                StudentId = students[5].Id,
                CourseId = courses[5].Id,
                Grade = 3.7m,
            },
            new()
            {
                StudentId = students[6].Id,
                CourseId = courses[6].Id,
                Grade = 2.9m,
            },
            new()
            {
                StudentId = students[7].Id,
                CourseId = courses[7].Id,
                Grade = 3.1m,
            },
            new()
            {
                StudentId = students[8].Id,
                CourseId = courses[8].Id,
                Grade = 3.5m,
            },
            new()
            {
                StudentId = students[9].Id,
                CourseId = courses[9].Id,
                Grade = 3.0m,
            },
            new()
            {
                StudentId = students[10].Id,
                CourseId = courses[10].Id,
                Grade = 2.7m,
            },
            new()
            {
                StudentId = students[11].Id,
                CourseId = courses[11].Id,
                Grade = 3.6m,
            },
            new()
            {
                StudentId = students[12].Id,
                CourseId = courses[12].Id,
                Grade = 2.4m,
            },
            new()
            {
                StudentId = students[13].Id,
                CourseId = courses[13].Id,
                Grade = 3.8m,
            },
            new()
            {
                StudentId = students[14].Id,
                CourseId = courses[14].Id,
                Grade = 3.2m,
            },
            new()
            {
                StudentId = students[15].Id,
                CourseId = courses[15].Id,
                Grade = 3.3m,
            },
            new()
            {
                StudentId = students[16].Id,
                CourseId = courses[16].Id,
                Grade = 2.6m,
            },
            new()
            {
                StudentId = students[17].Id,
                CourseId = courses[17].Id,
                Grade = 3.9m,
            },
            new()
            {
                StudentId = students[18].Id,
                CourseId = courses[18].Id,
                Grade = 3.4m,
            },
            new()
            {
                StudentId = students[19].Id,
                CourseId = courses[19].Id,
                Grade = 3.1m,
            },
        };

        context.Enrollments.AddRange(enrollments);
        context.SaveChanges();
    }
}

app.Run();
