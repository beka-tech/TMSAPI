using System.ComponentModel.DataAnnotations;
using TmsApi.Domain.Enums;

namespace TmsApi.Application.DTOs;

public sealed record UpdateEnrollmentStatusRequest(
    [property: Required]
    [property: EnumDataType(typeof(EnrollmentStatus))]
    EnrollmentStatus? Status
);

public sealed record UpdateEnrollmentGradeRequest(
    [property: Required]
    [property: Range(typeof(decimal), "0", "100", ErrorMessage = "Grade must be between 0 and 100.")]
    decimal? Grade
);
