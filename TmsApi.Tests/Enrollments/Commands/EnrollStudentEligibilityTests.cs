using NSubstitute;
using TmsApi.Application.Common;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Tests.Enrollments.Commands;

public sealed class EnrollStudentEligibilityTests
{
    [Theory]
    [InlineData(StudentEnrollmentEligibility.NotFound, "student_not_found")]
    [InlineData(StudentEnrollmentEligibility.Inactive, "student_inactive")]
    public async Task Handle_WhenStudentIsIneligible_ReturnsExpectedErrorWithoutLookingUpCourse(
        StudentEnrollmentEligibility eligibility,
        string expectedErrorCode
    )
    {
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();
        var cancellationToken = new CancellationTokenSource().Token;
        enrollmentService
            .GetStudentEligibilityAsync(42, cancellationToken)
            .Returns(Task.FromResult(eligibility));
        var handler = new EnrollStudentHandler(enrollmentService, courseService);

        var result = await handler.Handle(
            new EnrollStudentCommand(42, "CS-401"),
            cancellationToken
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        await courseService
            .DidNotReceive()
            .GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await enrollmentService
            .DidNotReceive()
            .ExistsAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await enrollmentService
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenStudentIsActiveAndCourseHasRoom_AddsEnrollment()
    {
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();
        var course = new Course
        {
            Id = 7,
            Code = "CS-401",
            Title = "Advanced Web Development",
            MaxCapacity = 30,
        };
        enrollmentService
            .GetStudentEligibilityAsync(42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(StudentEnrollmentEligibility.Eligible));
        courseService
            .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Course?>(course));
        enrollmentService
            .ExistsAsync(42, "CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));
        var handler = new EnrollStudentHandler(enrollmentService, courseService);

        var result = await handler.Handle(
            new EnrollStudentCommand(42, "CS-401"),
            CancellationToken.None
        );

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value.StudentId);
        Assert.Equal("CS-401", result.Value.CourseCode);
        await enrollmentService
            .Received(1)
            .AddAsync(
                Arg.Is<Enrollment>(enrollment =>
                    enrollment.StudentId == 42 && enrollment.CourseId == course.Id
                ),
                Arg.Any<CancellationToken>()
            );
    }
}
