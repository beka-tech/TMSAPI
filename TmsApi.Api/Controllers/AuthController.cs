using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using TmsApi.Infrastructure.Identity;
using TmsApi.Infrastructure.Persistence;
using TmsApi.Infrastructure.Services;

namespace TmsApi.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(UserManager<TmsUser> users, RoleManager<IdentityRole> roles,
    TmsDbContext db, TokenService tokens) : ControllerBase
{
    public record RegisterRequest(
        [Required, EmailAddress] string Email,
        [Required, MinLength(8), MaxLength(128)] string Password,
        [Required, MaxLength(100)] string FirstName,
        [Required, MaxLength(100)] string LastName,
        string? Role = null);

    [AllowAnonymous]
    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        if (request.Role is not null && request.Role != "Student")
            return BadRequest(new { detail = "Public registration only supports the Student role." });
        var response = new { message = "Registration request received." };
        if (await users.FindByEmailAsync(request.Email) is not null) return Ok(response);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serialize creation of the fixed role, including simultaneous first registrations.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(81723401)", ct);
        if (!await roles.RoleExistsAsync("Student"))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole("Student"));
            if (!roleResult.Succeeded) return Problem("Unable to configure student registration.");
        }
        var user = new TmsUser { UserName = request.Email, Email = request.Email,
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim() };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        result = await users.AddToRoleAsync(user, "Student");
        if (!result.Succeeded) return Problem("Unable to complete registration.");
        await transaction.CommitAsync(ct);
        return Ok(response);
    }

    public record LoginRequest([Required, EmailAddress] string Email,
        [Required, MaxLength(128)] string Password);

    [AllowAnonymous]
    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null) return Unauthorized(new { detail = "Invalid credentials." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(user.Id, ct);
        await db.Entry(user).ReloadAsync(ct);
        if (await users.IsLockedOutAsync(user)) return Unauthorized(new { detail = "Invalid credentials." });
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            await transaction.CommitAsync(ct);
            return Unauthorized(new { detail = "Invalid credentials." });
        }
        await users.ResetAccessFailedCountAsync(user);
        var pair = await IssuePairAsync(user, ct);
        await transaction.CommitAsync(ct);
        return Ok(pair);
    }

    public record RefreshRequest([Required, MaxLength(256)] string RefreshToken);

    [AllowAnonymous]
    [EnableRateLimiting("AuthLimiter")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var hash = HashToken(request.RefreshToken);
        var owner = await db.RefreshTokens.AsNoTracking().Where(t => t.Token == hash).Select(t => t.UserId).SingleOrDefaultAsync(ct);
        if (owner is null) return Unauthorized(new { detail = "Invalid refresh token." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(owner, ct);
        var user = await users.FindByIdAsync(owner);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.Token == hash, ct);
        if (user is null || stored is null) return Unauthorized();
        if (stored.IsUsed)
        {
            await RevokeSessionsAsync(user, ct);
            await transaction.CommitAsync(ct);
            return Unauthorized(new { detail = "Refresh token reuse detected. Sign in again." });
        }
        if (stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow || await users.IsLockedOutAsync(user))
            return Unauthorized(new { detail = "Refresh token expired or revoked." });
        stored.IsUsed = true;
        var pair = await IssuePairAsync(user, ct);
        await transaction.CommitAsync(ct);
        return Ok(pair);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockUserAsync(id, ct);
        var user = await users.FindByIdAsync(id);
        if (user is null) return Unauthorized();
        await db.Entry(user).ReloadAsync(ct);
        await RevokeSessionsAsync(user, ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }

    private Task<int> LockUserAsync(string id, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {id} FOR UPDATE", ct);

    private async Task RevokeSessionsAsync(TmsUser user, CancellationToken ct)
    {
        await db.RefreshTokens.Where(t => t.UserId == user.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true), ct);
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded) throw new InvalidOperationException("Unable to revoke sessions.");
    }

    private async Task<object> IssuePairAsync(TmsUser user, CancellationToken ct)
    {
        var raw = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        db.RefreshTokens.Add(new RefreshToken { Token = HashToken(raw), UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7) });
        await db.SaveChangesAsync(ct);
        return new { accessToken = tokens.GenerateJwt(user, await users.GetRolesAsync(user)), refreshToken = raw };
    }

    public static string HashToken(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
