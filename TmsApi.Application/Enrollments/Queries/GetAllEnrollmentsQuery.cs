using MediatR;
using TmsApi.Application.DTOs;

namespace TmsApi.Application.Enrollments.Queries;

public record GetAllEnrollmentsQuery(int Page = 1, int PageSize = 20, int? StudentId = null, int? CourseId = null, TmsApi.Domain.Enums.EnrollmentStatus? Status = null) : IRequest<List<EnrollmentListDto>>;
