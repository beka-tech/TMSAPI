using System.ComponentModel.DataAnnotations;

namespace TMSAPI.Dtos;

public record EnrollStudentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "StudentId must be a Positive Integer. ")]
    public required int StudentId { get; init; }
}
