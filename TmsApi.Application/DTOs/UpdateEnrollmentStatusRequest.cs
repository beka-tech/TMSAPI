using TmsApi.Domain.Enums;

namespace TmsApi.Application.DTOs;

public sealed record UpdateEnrollmentStatusRequest(EnrollmentStatus Status);
