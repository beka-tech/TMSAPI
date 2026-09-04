using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

public sealed record CreateStudentRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string RegistrationNumber { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "4", ErrorMessage = "GPA must be between 0 and 4.")]
    public decimal GPA { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record UpdateStudentRequest
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string RegistrationNumber { get; init; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(typeof(decimal), "0", "4", ErrorMessage = "GPA must be between 0 and 4.")]
    public decimal GPA { get; init; }

    public bool? IsActive { get; init; }
}

public sealed record StudentResponseDto(
    int Id,
    string RegistrationNumber,
    string Name,
    decimal GPA,
    bool IsActive
);
