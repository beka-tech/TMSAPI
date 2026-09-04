// using NSubstitute;
// using TmsApi.Application.Common;
// using TmsApi.Application.Enrollments.Commands;
// using TmsApi.Application.Interfaces;
// using TmsApi.Domain.Entities;

// namespace TmsApi.Tests;

// public class EnrollStudentHandlerTests
// {
//     [Fact]
//     public async Task Handle_WhenAlreadyEnrolled_ReturnsDuplicateError()
//     {
//         // Arrange: create a mock IEnrollmentService (Application-layer interface)var enrollmentService = Substitute.For<IEnrollmentService>();
//         var courseService = Substitute.For<ICourseService>();
//         enrollmentService
//             .ExistsAsync(99, "CS-401", Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult(true));
//         // Course lookup runs first in the handler; return any non-null// course so the duplicate check is the branch under test.
//         var course = new Course
//         {
//             Id = 1,
//             Code = "CS-401",
//             Title = "Advanced Web Dev",
//             MaxCapacity = 30,
//             Enrollments = new List<Enrollment>(),
//         };
//         courseService
//             .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult<Course?>(course));
//         var handler = new EnrollStudentHandler(enrollmentService, courseService);
//         var command = new EnrollStudentCommand(StudentId: 99, CourseCode: "CS-401");
//         // Act
//         var result = await handler.Handle(command, CancellationToken.None); // Assert: handler surfaces the duplicate without touching the database.// Assert on the machine-readable Code (the contract) plus full record
//         // equality, NOT on the human-readable Message,see M7 sealed-record pattern.Assert.False(result.IsSuccess);
//         Assert.Equal("already_enrolled", result.Error.Code);
//         Assert.Equal(EnrollmentError.AlreadyEnrolled(99, "CS-401"), result.Error);
//         // The duplicate branch never writes — prove it.
//         await enrollmentService
//             .DidNotReceive()
//             .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
//     }

//     [Fact]
//     public async Task Handle_WhenCourseFull_ReturnsCapacityError()
//     {
//         // Arrange: course is at capacity (Enrollments.Count >= MaxCapacity).// M7's handler checks capacity against the course object, not via service// calls.
//         var enrollmentService = Substitute.For<IEnrollmentService>();
//         var courseService = Substitute.For<ICourseService>();
//         var course = new Course
//         {
//             Id = 1,
//             Code = "CS-401",
//             Title = "Advanced Web Dev",
//             MaxCapacity = 35,
//             Enrollments = Enumerable
//                 .Range(1, 35)
//                 .Select(i => new Enrollment
//                 {
//                     Id = i,
//                     CourseId = 1,
//                     Status = "Pending",
//                 })
//                 .ToList(),
//         };
//         courseService
//             .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult<Course?>(course));
//         var handler = new EnrollStudentHandler(enrollmentService, courseService);
//         var command = new EnrollStudentCommand(StudentId: 100, CourseCode: "CS-401"); // Act
//         var result = await handler.Handle(command, CancellationToken.None); // Assert: typed error matches the M7 sealed-record factory
//         Assert.False(result.IsSuccess);
//         Assert.Equal("course_full", result.Error.Code);
//         Assert.Equal(EnrollmentError.CourseFull("Advanced Web Dev", 35), result.Error);
//         await enrollmentService
//             .DidNotReceive()
//             .AddAsync(Arg.Any<Enrollment>(), Arg.Any<CancellationToken>());
//     }

//     [Fact]
//     public async Task Handle_SuccessfulPath_AddsEnrollmentOnce()
//     {
//         // Arrange: course has room, student is not already enrolled; expect one// AddAsync call.
//         var enrollmentService = Substitute.For<IEnrollmentService>();
//         var courseService = Substitute.For<ICourseService>();
//         var course = new Course
//         {
//             Id = 1,
//             Code = "CS-401",
//             Title = "Advanced Web Dev",
//             MaxCapacity = 35,
//             Enrollments = Enumerable
//                 .Range(1, 20)
//                 .Select(i => new Enrollment
//                 {
//                     Id = i,
//                     CourseId = 1,
//                     Status = "Pending",
//                 })
//                 .ToList(),
//         };
//         courseService
//             .GetByCodeAsync("CS-401", Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult<Course?>(course));
//         enrollmentService
//             .ExistsAsync(100, "CS-401", Arg.Any<CancellationToken>())
//             .Returns(Task.FromResult(false));
//         var handler = new EnrollStudentHandler(enrollmentService, courseService);
//         var command = new EnrollStudentCommand(StudentId: 100, CourseCode: "CS-401"); // Act
//         var result = await handler.Handle(command, CancellationToken.None); // Assert: handler produced a typed success payload with the rightIDsAssert.True(result.IsSuccess);
//         Assert.Equal(100, result.Value.StudentId);
//         Assert.Equal("CS-401", result.Value.CourseCode);
//         // The interaction: AddAsync called exactly once with a row that pointsat// the student and course
//         await enrollmentService
//             .Received(1)
//             .AddAsync(
//                 Arg.Is<Enrollment>(e => e.StudentId == 100 && e.CourseId == 1),
//                 Arg.Any<CancellationToken>()
//             );
//     }
// }
