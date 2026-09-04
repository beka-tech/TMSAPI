using System.ComponentModel.DataAnnotations;
using TmsApi.Application.DTOs;

namespace TmsApi.Tests.Students;

public sealed class StudentModelsValidationTests
{
    public static TheoryData<decimal, bool> GpaCases =>
        new()
        {
            { 0m, true },
            { 4m, true },
            { -0.01m, false },
            { 4.01m, false },
        };

    [Theory]
    [InlineData(1, 1)]
    [InlineData(20, 100)]
    public void StudentRequests_AtStringLengthBoundaries_AreValid(
        int registrationNumberLength,
        int nameLength
    )
    {
        var requests = CreateRequests(
            new string('R', registrationNumberLength),
            new string('N', nameLength),
            2.5m
        );

        Assert.All(requests, request => Assert.Empty(Validate(request)));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\r\n")]
    public void StudentRequests_WithWhitespaceOnlyStrings_AreInvalid(string whitespace)
    {
        var requests = CreateRequests(whitespace, whitespace, 2.5m);

        Assert.All(
            requests,
            request =>
            {
                var results = Validate(request);

                Assert.Contains(
                    results,
                    result => result.MemberNames.Contains(nameof(CreateStudentRequest.RegistrationNumber))
                );
                Assert.Contains(
                    results,
                    result => result.MemberNames.Contains(nameof(CreateStudentRequest.Name))
                );
            }
        );
    }

    [Fact]
    public void StudentRequests_AboveStringLengthLimits_AreInvalid()
    {
        var requests = CreateRequests(new string('R', 21), new string('N', 101), 2.5m);

        Assert.All(
            requests,
            request =>
            {
                var results = Validate(request);

                Assert.Contains(
                    results,
                    result => result.MemberNames.Contains(nameof(CreateStudentRequest.RegistrationNumber))
                );
                Assert.Contains(
                    results,
                    result => result.MemberNames.Contains(nameof(CreateStudentRequest.Name))
                );
            }
        );
    }

    [Theory]
    [MemberData(nameof(GpaCases))]
    public void StudentRequests_ValidateGpaBoundaries(decimal gpa, bool expectedValid)
    {
        var requests = CreateRequests("REG-001", "Student Name", gpa);

        Assert.All(
            requests,
            request =>
            {
                var hasGpaError = Validate(request)
                    .Any(result => result.MemberNames.Contains(nameof(CreateStudentRequest.GPA)));

                Assert.Equal(expectedValid, !hasGpaError);
            }
        );
    }

    private static object[] CreateRequests(string registrationNumber, string name, decimal gpa) =>
        [
            new CreateStudentRequest
            {
                RegistrationNumber = registrationNumber,
                Name = name,
                GPA = gpa,
            },
            new UpdateStudentRequest
            {
                RegistrationNumber = registrationNumber,
                Name = name,
                GPA = gpa,
                IsActive = true,
            },
        ];

    private static IReadOnlyList<ValidationResult> Validate(object request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            results,
            validateAllProperties: true
        );
        return results;
    }
}
