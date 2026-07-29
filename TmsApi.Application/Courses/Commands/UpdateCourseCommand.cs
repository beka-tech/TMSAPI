using MediatR;

namespace TmsApi.Application.Courses.Commands;

public record UpdateCourseCommand(string Code, string Title, int MaxCapacity) : IRequest<bool>;
