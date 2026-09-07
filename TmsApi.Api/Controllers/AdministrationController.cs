using System.Security.Claims;
using System.Text.Json;
using TmsApi.Domain.Entities;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;

namespace TmsApi.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/administration")]
public class AdministrationController(TmsDbContext db, UserManager<TmsUser> users, RoleManager<IdentityRole> roles) : ControllerBase
{
    public record AssignInstructorRequest([Required] string UserId);
    public record LinkStudentRequest([Range(1, int.MaxValue)] int StudentId);
    public record AssignRoleRequest([Required, RegularExpression("^(Student|Instructor|Admin)$")] string Role);

    [HttpPut("courses/{id:int}/instructor")]
    public async Task<IActionResult> AssignInstructor(int id, AssignInstructorRequest request, CancellationToken ct)
    {
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == id, ct);
        var user = await users.FindByIdAsync(request.UserId);
        if (course is null || user is null) return NotFound();
        if (!await users.IsInRoleAsync(user, "Instructor")) return BadRequest(new { detail = "User must have the Instructor role." });
        course.InstructorId = user.Id;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("users/{id}/student")]
    public async Task<IActionResult> LinkStudent(string id, LinkStudentRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null || !await db.Students.AnyAsync(s => s.Id == request.StudentId, ct)) return NotFound();
        if (!await users.IsInRoleAsync(user, "Student")) return BadRequest(new { detail = "User must have the Student role." });
        if (await db.Users.AnyAsync(u => u.StudentId == request.StudentId && u.Id != id, ct)) return Conflict();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        await db.Entry(user).ReloadAsync(ct);
        db.AuditEntries.Add(new AuditEntry { ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            EntityType = "User", EntityId = id, Action = "LinkStudent",
            Changes = JsonSerializer.Serialize(new { Before = user.StudentId, After = request.StudentId }) });
        user.StudentId = request.StudentId;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return Conflict(new { errors = result.Errors.Select(e => e.Description) });
        if (!(await users.UpdateSecurityStampAsync(user)).Succeeded) return Conflict();
        await db.RefreshTokens.Where(t => t.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("users/{id}/roles")]
    public async Task<IActionResult> AssignRole(string id, AssignRoleRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE", ct);
        await db.Entry(user).ReloadAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81723401)", ct);
        if (!await roles.RoleExistsAsync(request.Role))
        {
            var creation = await roles.CreateAsync(new IdentityRole(request.Role));
            if (!creation.Succeeded) return Conflict();
        }
        if (!await users.IsInRoleAsync(user, request.Role))
        {
            var result = await users.AddToRoleAsync(user, request.Role);
            if (!result.Succeeded) return Conflict();
        }
        db.AuditEntries.Add(new AuditEntry { ActorId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            EntityType = "User", EntityId = id, Action = "AssignRole",
            Changes = JsonSerializer.Serialize(new { Role = request.Role }) });
        var stamp = await users.UpdateSecurityStampAsync(user);
        if (!stamp.Succeeded) return Conflict();
        await db.RefreshTokens.Where(t => t.UserId == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
