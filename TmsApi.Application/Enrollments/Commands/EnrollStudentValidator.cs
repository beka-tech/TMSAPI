using FluentValidation;

namespace TmsApi.Application.Enrollments.Commands;

public class EnrollStudentValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentValidator()
    {
        RuleFor(x => x.StudentId)
            .GreaterThan(0)
            .WithMessage("Student ID must be a positive number.");
        RuleFor(x => x.CourseCode).NotEmpty().WithMessage("Course code is required.");
        RuleFor(x => x.CourseCode)
            .Matches(@"^[A-Z]{3}-\d{3}$")
            .WithMessage("Course code must follow the format XXX-000(e.g., CSE-101).");
    }
}
