using System.Security.Claims;
using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TmsApi.Api.Authorization;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;

namespace TmsApi.Api.Controllers.V2;

[Authorize]
[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/transcripts")]
public class TranscriptsController(ITranscriptStatusStore store, TmsAccess access) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    public async Task<IActionResult> RequestTranscript(TranscriptRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (!await access.OwnsStudentAsync(User, request.StudentId, ct)) return Forbid();
        if (idempotencyKey?.Length > 128) return BadRequest(new { detail = "Idempotency-Key is limited to 128 characters." });
        var job = await store.RequestAsync(request.StudentId, User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey, ct);
        Response.Headers.RetryAfter = "5";
        return Accepted(Url.Action(nameof(GetStatus), new { version = "2.0", id = job.Id }), ITranscriptStatusStore.Status(job));
    }

    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id, CancellationToken ct)
    {
        var job = await store.GetJobAsync(id, ct);
        if (job is null) return NotFound();
        if (!await access.OwnsStudentAsync(User, job.StudentId, ct)) return Forbid();
        return Ok(ITranscriptStatusStore.Status(job));
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id, CancellationToken ct)
    {
        var job = await store.GetJobAsync(id, ct);
        if (job is null) return NotFound();
        if (!await access.OwnsStudentAsync(User, job.StudentId, ct)) return Forbid();
        if (job.State != "Ready" || job.Content is null) return Conflict(new { detail = "Transcript is not ready." });
        Response.Headers.CacheControl = "no-store";
        return File(Encoding.UTF8.GetBytes(job.Content), "text/plain; charset=utf-8", $"transcript-{job.Id}.txt");
    }
}
