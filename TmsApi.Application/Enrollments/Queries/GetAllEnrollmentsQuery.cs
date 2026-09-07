using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Enrollments.Queries;

public record GetAllEnrollmentsQuery() : IRequest<List<EnrollmentListDto>>;
