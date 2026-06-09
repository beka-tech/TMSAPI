
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// builder.Services.AddAuthentication();
// builder.Services.AddAuthentication("Bearer")
//     .AddJwtBearer();

builder.Services
.AddAuthentication("Training")
.AddScheme<AuthenticationSchemeOptions,
TrainingAuthHandler>("Training", null);



builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// app.Run();



app.MapGet("/api/assessments/results", () => Results.Ok(new
{
  courseCode = "CS-101",
  studentId = "S-001",
  letterGrade = "A"
})).RequireAuthorization() ;





app.Run();

