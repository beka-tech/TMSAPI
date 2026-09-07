using System.Threading.Channels;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;

namespace TmsApi.Api.Controllers.V2;

[ApiController]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/transcripts")]
public class TranscriptsController(
    Channel<TranscriptRequest> channel,
    ITranscriptStatusStore statusStore
) : ControllerBase
{
    // POST /api/v2/transcripts
    // Accepts a transcript request and queues it for background processing.
    [HttpPost]
    [EnableRateLimiting("transcripts")]
    public async Task<IActionResult> RequestTranscript(
        TranscriptRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct
    )
    {
        // 1. Prevent duplicate jobs when the same Idempotency-Key is reused.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existingReportId = await statusStore.GetReportIdForIdempotencyKeyAsync(
                idempotencyKey,
                ct
            );

            if (existingReportId is not null)
            {
                // Return the existing job instead of creating another one.
                var existingStatus = await statusStore.GetAsync(existingReportId, ct);

                return Accepted(
                    Url.Action(nameof(GetStatus), new { version = "2.0", id = existingReportId }),
                    existingStatus
                );
            }
        }

        // 2. Create a short unique ID for this transcript job.
        var reportId = Guid.NewGuid().ToString("N")[..12];

        // 3. Store the initial job status (normally "Queued").
        var status = await statusStore.CreateAsync(reportId, request.StudentId, ct);

        // 4. Link the Idempotency-Key to this job for future duplicate checks.
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await statusStore.LinkIdempotencyKeyAsync(idempotencyKey, reportId, ct);
        }

        // 5. Add the job to the Channel.
        // TranscriptWorker will process it in the background.
        await channel.Writer.WriteAsync(request.WithReportId(reportId), ct);

        // Tell the client to wait 5 seconds before checking status.
        Response.Headers.RetryAfter = "5";

        // 202 = accepted, but background processing is not finished yet.
        return Accepted(
            Url.Action(nameof(GetStatus), new { version = "2.0", id = reportId }),
            status
        );
    }

    // GET /api/v2/transcripts/{id}/status
    // Returns the current state of a transcript job.
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetStatus(string id, CancellationToken ct)
    {
        // Find the job by ReportId.
        var status = await statusStore.GetAsync(id, ct);

        // Unknown ReportId → 404.
        if (status is null)
        {
            return NotFound(
                new ProblemDetails
                {
                    Title = "Transcript not found",
                    Detail = $"No transcript request with id '{id}'.",
                    Status = StatusCodes.Status404NotFound,
                }
            );
        }

        // Existing job → return its current status.
        return Ok(status);
    }
}
