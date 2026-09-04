using System.ComponentModel.DataAnnotations;
using TmsApi.Application.DTOs;
using TmsApi.Domain.Enums;

namespace TmsApi.Tests.Enrollments;

public sealed class EnrollmentUpdateRequestValidationTests
{
    public static TheoryData<decimal?> InvalidGrades =>
        new()
        {
            { null },
            { -0.01m },
            { 100.01m },
        };

    [Fact]
    public void UpdateStatus_WithNullStatus_IsInvalid()
    {
        var results = Validate(new UpdateEnrollmentStatusRequest(null));

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(UpdateEnrollmentStatusRequest.Status))
        );
    }

    [Fact]
    public void UpdateStatus_WithUndefinedStatus_IsInvalid()
    {
        var results = Validate(new UpdateEnrollmentStatusRequest((EnrollmentStatus)999));

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(UpdateEnrollmentStatusRequest.Status))
        );
    }

    [Fact]
    public void UpdateGrade_AtInclusiveBoundaries_IsValid()
    {
        Assert.Empty(Validate(new UpdateEnrollmentGradeRequest(0m)));
        Assert.Empty(Validate(new UpdateEnrollmentGradeRequest(100m)));
    }

    [Theory]
    [MemberData(nameof(InvalidGrades))]
    public void UpdateGrade_WithMissingOrOutOfRangeValue_IsInvalid(decimal? grade)
    {
        var results = Validate(new UpdateEnrollmentGradeRequest(grade));

        Assert.Contains(
            results,
            result => result.MemberNames.Contains(nameof(UpdateEnrollmentGradeRequest.Grade))
        );
    }

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
