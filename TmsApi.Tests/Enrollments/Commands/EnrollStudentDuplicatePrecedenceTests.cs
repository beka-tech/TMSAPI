using NSubstitute;
using TmsApi.Application.Common;
using TmsApi.Application.Enrollments.Commands;
using TmsApi.Application.Interfaces;
using TmsApi.Domain.Entities;

namespace TmsApi.Tests.Enrollments.Commands;

public sealed class EnrollStudentDuplicatePrecedenceTests
{
    [Fact]
    public async Task Handle_WhenEnrollmentExistsInFullCourse_ReturnsAlreadyEnrolledWithoutAdding()
    {
        var enrollmentService = Substitute.For<IEnrollmentService>();
        var courseService = Substitute.For<ICourseService>();
        var course = new Course
        {
            Id = 7,
            Code = "CS-401",
            Title = "Advanced Web Development",
            MaxCapacity = 1,
            Enrollments =
            [
                new Enrollment
                {
                    Id = 10,
                    StudentId = 99,
                    CourseId = 7,
                },
            ],
        };
        enrollmentService
            .GetStudentEligibilityAsync(42, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(StudentEnrollmentEligibility.Eligible));
        courseService
            .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Course?>(course));
        enrollmentService
            .ExistsAsync(42, "CS-401", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        var handler = new EnrollStudentHandler(enrollmentService, courseService);

        var result = await handler.Handle(
            new EnrollStudentCommand(42, "CS-401"),
            CancellationToken.None
        );

        Assert.False(result.IsSuccess);
        Assert.Equal(EnrollmentError.AlreadyEnrolled(42, "CS-401"), result.Error);
        await enrollmentService
            .Received(1)
            .ExistsAsync(42, "CS-401", Arg.Any<CancellationToken>());
        await enrollmentService
            .DidNotReceive()
            .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
    }
}
