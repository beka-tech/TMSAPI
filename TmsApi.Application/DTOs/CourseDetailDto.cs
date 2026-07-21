// using TmsApi.Application.Dtos;

// using TMSAPI.Dtos;

namespace TmsApi.Application.DTOs;

public record CourseDetailDto
{
    public required int Id { get; init; }

    public required string Code { get; init; }

    public required string Title { get; init; }

    public required int MaxCapacity { get; init; }

    public required int EnrollmentCount { get; init; }

    // public required IReadOnlyList<TMSAPI.DTOs.LinkDto> Links { get; init; }
    public required IReadOnlyList<LinkDto> Links { get; init; }
}
