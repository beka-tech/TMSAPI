namespace TmsApi.Application.Common;

public sealed record EnrollmentError(string Code, string Message)
{
    public static EnrollmentError CourseNotFound(string code) =>
        new("course_not_found", $"Course '{code}' was not found.");

    public static EnrollmentError CourseFull(string title, int capacity) =>
        new("course_full", $"Course '{title}' is full (capacity {capacity}).");

    public static EnrollmentError AlreadyEnrolled(int studentId, string code) =>
        new("already_enrolled", $"Student {studentId} is already enrolled in {code}.");
}
