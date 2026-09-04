using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TmsApi.Application.DTOs;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;
using DataAnnotationsValidationException = System.ComponentModel.DataAnnotations.ValidationException;

namespace TmsApi.Tests.Students;

public sealed class StudentServiceValidationTests
{
    [Fact]
    public async Task CreateAsync_InvalidInput_IsRejectedBeforeDatabaseAccess()
    {
        await using var context = CreateContext();
        var service = new StudentService(context, NullLogger<StudentService>.Instance);
        var request = new CreateStudentRequest
        {
            RegistrationNumber = "REG-001",
            Name = " ",
            GPA = 4.01m,
        };

        await Assert.ThrowsAsync<DataAnnotationsValidationException>(
            () => service.CreateAsync(request)
        );
    }

    [Fact]
    public async Task UpdateAsync_InvalidInput_IsRejectedBeforeDatabaseAccess()
    {
        await using var context = CreateContext();
        var service = new StudentService(context, NullLogger<StudentService>.Instance);
        var request = new UpdateStudentRequest
        {
            RegistrationNumber = "REG-001",
            Name = "Student Name",
            GPA = -0.01m,
        };

        await Assert.ThrowsAsync<DataAnnotationsValidationException>(
            () => service.UpdateAsync(1, request)
        );
    }

    private static TmsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<TmsDbContext>()
            .UseNpgsql("Host=localhost;Database=tms_validation_test;Username=postgres")
            .Options;

        return new TmsDbContext(options);
    }
}
