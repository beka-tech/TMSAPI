using MediatR;

namespace TmsApi.Application.Enrollments.Commands;

public record ApproveEnrollmentCommand(int EnrollmentId) : IRequest;
