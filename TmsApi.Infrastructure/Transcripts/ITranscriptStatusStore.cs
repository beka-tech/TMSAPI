using TmsApi.Application.Transcripts;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Transcripts;

public interface ITranscriptStatusStore
{
    Task<TranscriptJob> RequestAsync(int studentId, string requestedBy, string? idempotencyKey, CancellationToken ct);
    Task<TranscriptJob?> GetJobAsync(string reportId, CancellationToken ct);
    Task<bool> ProcessNextAsync(CancellationToken ct);
    static TranscriptStatus Status(TranscriptJob job) => new(job.Id, job.StudentId,
        Enum.Parse<TranscriptState>(job.State), job.RequestedAt, job.StartedAt, job.CompletedAt,
        job.State == "Ready" ? $"/api/v2/transcripts/{job.Id}/download" : null, job.ErrorMessage);
}
