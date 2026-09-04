using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using TmsApi.Api.Controllers.V2;
using TmsApi.Application.Common;
using TmsApi.Application.DTOs;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Enums;

namespace TmsApi.Tests.Enrollments;

public sealed class EnrollmentControllerTests
{
    [Fact]
    public void Controller_ExposesRequiredV2Routes()
    {
        var controllerType = typeof(EnrollmentsController);
        var route = Assert.Single(controllerType.GetCustomAttributes<RouteAttribute>());

        Assert.Equal("api/v{version:apiVersion}/enrollments", route.Template);
        AssertRoute<HttpGetAttribute>(nameof(EnrollmentsController.GetAll), null);
        AssertRoute<HttpGetAttribute>(
            nameof(EnrollmentsController.GetByStudent),
            "student/{studentId:int}"
        );
        AssertRoute<HttpPostAttribute>(nameof(EnrollmentsController.Enroll), null);
        AssertRoute<HttpPatchAttribute>(
            nameof(EnrollmentsController.UpdateStatus),
            "{id:int}/status"
        );
        AssertRoute<HttpPatchAttribute>(
            nameof(EnrollmentsController.UpdateGrade),
            "{id:int}/grade"
        );
    }

    [Theory]
    [InlineData("student_not_found", 404)]
    [InlineData("course_not_found", 404)]
    [InlineData("already_enrolled", 409)]
    [InlineData("course_full", 400)]
    [InlineData("student_inactive", 400)]
    public async Task Enroll_MapsDomainErrorToRequiredStatus(string errorCode, int expectedStatus)
    {
        var mediator = Substitute.For<IMediator>();
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var error = new EnrollmentError(errorCode, "Enrollment rejected for test.");
        mediator
            .Send(Arg.Any<EnrollStudentCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result<EnrollmentCreated, EnrollmentError>.Failure(error));
        var controller = new EnrollmentsController(mediator, enrollmentService);

        var result = await controller.Enroll(
            new EnrollStudentCommand(12, "CS-401"),
            CancellationToken.None
        );

        var response = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatus, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WhenEnrollmentDoesNotExist_ReturnsNotFound()
    {
        var mediator = Substitute.For<IMediator>();
        var enrollmentService = Substitute.For<IEnrollmentService>();
        enrollmentService
            .UpdateStatusAsync(99, EnrollmentStatus.Completed, Arg.Any<CancellationToken>())
            .Returns(false);
        var controller = new EnrollmentsController(mediator, enrollmentService);

        var result = await controller.UpdateStatus(
            99,
            new UpdateEnrollmentStatusRequest(EnrollmentStatus.Completed),
            CancellationToken.None
        );

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task UpdateGrade_WhenEnrollmentExists_ReturnsNoContent()
    {
        var mediator = Substitute.For<IMediator>();
        var enrollmentService = Substitute.For<IEnrollmentService>();
        enrollmentService.UpdateGradeAsync(7, 87.5m, Arg.Any<CancellationToken>()).Returns(true);
        var controller = new EnrollmentsController(mediator, enrollmentService);

        var result = await controller.UpdateGrade(
            7,
            new UpdateEnrollmentGradeRequest(87.5m),
            CancellationToken.None
        );

        Assert.IsType<NoContentResult>(result);
    }

    private static void AssertRoute<TAttribute>(string methodName, string? expectedTemplate)
        where TAttribute : HttpMethodAttribute
    {
        var method = typeof(EnrollmentsController).GetMethod(
            methodName,
            BindingFlags.Public | BindingFlags.Instance
        );

        Assert.NotNull(method);
        var attribute = Assert.Single(method.GetCustomAttributes<TAttribute>());
        Assert.Equal(expectedTemplate, attribute.Template);
    }
}
