using System.ComponentModel.DataAnnotations;

namespace TmsApi.Application.DTOs;

public record UpdateCourseDto
{
    [Required, MaxLength(200)]
    public required string Title { get; init; }
}
