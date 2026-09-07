using System.ComponentModel.DataAnnotations;
using TmsApi.Domain.Enums;

namespace TmsApi.Application.DTOs;

public sealed record UpdateEnrollmentStatusRequest
{
    public UpdateEnrollmentStatusRequest() { }
    public UpdateEnrollmentStatusRequest(EnrollmentStatus? status) => Status = status;
    [Required, EnumDataType(typeof(EnrollmentStatus))]
    public EnrollmentStatus? Status { get; init; }
}

public sealed record UpdateEnrollmentGradeRequest
{
    public UpdateEnrollmentGradeRequest() { }
    public UpdateEnrollmentGradeRequest(decimal? grade) => Grade = grade;
    [Required, Range(typeof(decimal), "0", "100", ErrorMessage = "Grade must be between 0 and 100.")]
    public decimal? Grade { get; init; }
}
