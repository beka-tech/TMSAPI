using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using TmsApi.Api.Controllers;
using TmsApi.Api.Hubs;
using TmsApi.Api.RateLimiting;
using TmsApi.Domain;
using TmsApi.Domain.Enums;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Tests.Security;

public class AccessControlTests
{
    [Theory]
    [InlineData(typeof(TmsApi.Api.Controllers.V2.EnrollmentsController))]
    [InlineData(typeof(TmsApi.Api.Controllers.V2.StudentsController))]
    [InlineData(typeof(TmsApi.Api.Controllers.V2.TranscriptsController))]
    [InlineData(typeof(TmsApi.Api.Controllers.EnrollmentsController))]
    [InlineData(typeof(TmsApi.Api.Controllers.V1.StudentsController))]
    [InlineData(typeof(TmsHub))]
    public void SensitiveResources_RequireAuthentication(Type resource) =>
        Assert.NotEmpty(resource.GetCustomAttributes<AuthorizeAttribute>());

    [Fact]
    public async Task Registration_CannotGrantPrivilegedRole()
    {
        var controller = new AuthController(null!, null!, null!, null!);
        var result = await controller.Register(new AuthController.RegisterRequest("a@example.test", "TestPass123", "A", "B", "Admin"), default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public void ArbitraryApiKeys_CannotCreateNewRateLimitPartitions()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Loopback;
        var anonymous = ApiKeyResolver.Resolve(context);
        foreach (var key in new[] { "tms-paid-001", "random-1", "random-2" })
        {
            context.Request.Headers["X-Api-Key"] = key;
            Assert.Equal(anonymous, ApiKeyResolver.Resolve(context));
        }
    }

    [Theory]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Approved, true)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Rejected, true)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Completed, true)]
    [InlineData(EnrollmentStatus.Completed, EnrollmentStatus.Approved, false)]
    [InlineData(EnrollmentStatus.Rejected, EnrollmentStatus.Approved, false)]
    [InlineData(EnrollmentStatus.Pending, EnrollmentStatus.Completed, false)]
    [InlineData(EnrollmentStatus.Approved, EnrollmentStatus.Approved, false)]
    public void Transitions_EnforceLifecycle(EnrollmentStatus from, EnrollmentStatus to, bool expected) =>
        Assert.Equal(expected, EnrollmentRules.CanTransition(from, to));
}
