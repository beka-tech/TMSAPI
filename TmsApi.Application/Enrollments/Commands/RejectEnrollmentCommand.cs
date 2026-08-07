using MediatR;

namespace TmsApi.Application.Enrollments.Commands;

public record RejectEnrollmentCommand(int EnrollmentId) : IRequest;
