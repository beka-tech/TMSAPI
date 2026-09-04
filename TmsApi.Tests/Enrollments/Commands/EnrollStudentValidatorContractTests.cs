using TmsApi.Application.Enrollments.Commands;

namespace TmsApi.Tests.Enrollments.Commands;

public sealed class EnrollStudentValidatorContractTests
{
    private readonly EnrollStudentValidator _validator = new();

    [Fact]
    public void Validate_WithCs401CourseCode_AcceptsCommand()
    {
        var result = _validator.Validate(new EnrollStudentCommand(42, "CS-401"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("CS401")]
    [InlineData("cs-401")]
    [InlineData("C-401")]
    [InlineData("CSCI-401")]
    [InlineData("CS-40")]
    [InlineData("CS-AB1")]
    [InlineData(" CS-401 ")]
    public void Validate_WithMalformedCourseCode_RejectsCommand(string? courseCode)
    {
        var result = _validator.Validate(new EnrollStudentCommand(42, courseCode!));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(EnrollStudentCommand.CourseCode)
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WithNonPositiveStudentId_RejectsCommand(int studentId)
    {
        var result = _validator.Validate(new EnrollStudentCommand(studentId, "CS-401"));

        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            failure => failure.PropertyName == nameof(EnrollStudentCommand.StudentId)
        );
    }
}
