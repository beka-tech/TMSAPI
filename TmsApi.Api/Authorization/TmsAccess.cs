using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Authorization;

public sealed class TmsAccess(TmsDbContext db)
{
    public async Task<int?> StudentIdAsync(ClaimsPrincipal user, CancellationToken ct) =>
        await db.Users.Where(u => u.Id == user.FindFirstValue(ClaimTypes.NameIdentifier))
            .Select(u => u.StudentId).SingleOrDefaultAsync(ct);

    public async Task<bool> OwnsStudentAsync(ClaimsPrincipal user, int studentId, CancellationToken ct) =>
        user.IsInRole("Admin") || (user.IsInRole("Student") && await StudentIdAsync(user, ct) == studentId &&
            await db.Students.AnyAsync(s => s.Id == studentId, ct));

    public Task<bool> ManagesCourseAsync(ClaimsPrincipal user, int courseId, CancellationToken ct) =>
        user.IsInRole("Admin") ? Task.FromResult(true) :
        user.IsInRole("Instructor") ? db.Courses.AnyAsync(c => c.Id == courseId &&
            c.InstructorId == user.FindFirstValue(ClaimTypes.NameIdentifier), ct) : Task.FromResult(false);
}
