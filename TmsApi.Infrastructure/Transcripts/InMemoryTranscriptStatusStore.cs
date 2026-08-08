using System.Collections.Concurrent;
using TmsApi.Application.Transcripts;

namespace TmsApi.Infrastructure.Transcripts;

public class InMemoryTranscriptStatusStore : ITranscriptStatusStore
{
    private readonly ConcurrentDictionary<string, TranscriptStatus> _byReportId = new();
    private readonly ConcurrentDictionary<string, string> _idempotencyToReportId = new();

    public Task<TranscriptStatus> CreateAsync(string reportId, int studentId, CancellationToken ct)
    {
        var status = new TranscriptStatus(
            reportId,
            studentId,
            TranscriptState.Queued,
            RequestedAt: DateTimeOffset.UtcNow
        );

        _byReportId[reportId] = status;

        return Task.FromResult(status);
    }

    public Task MarkProcessingAsync(string reportId, CancellationToken ct) =>
        Transition(
            reportId,
            current =>
                current with
                {
                    State = TranscriptState.Processing,
                    StartedAt = DateTimeOffset.UtcNow,
                },
            allowedFrom: TranscriptState.Queued
        );

    public Task MarkReadyAsync(string reportId, string downloadUrl, CancellationToken ct) =>
        Transition(
            reportId,
            current =>
                current with
                {
                    State = TranscriptState.Ready,
                    CompletedAt = DateTimeOffset.UtcNow,
                    DownloadUrl = downloadUrl,
                },
            allowedFrom: TranscriptState.Processing
        );

    public Task MarkFailedAsync(string reportId, string error, CancellationToken ct) =>
        Transition(
            reportId,
            current =>
                current with
                {
                    State = TranscriptState.Failed,
                    CompletedAt = DateTimeOffset.UtcNow,
                    ErrorMessage = error,
                },
            allowedFrom: TranscriptState.Processing
        );

    public Task<TranscriptStatus?> GetAsync(string reportId, CancellationToken ct) =>
        Task.FromResult(_byReportId.TryGetValue(reportId, out var s) ? s : null);

    public Task<string?> GetReportIdForIdempotencyKeyAsync(string key, CancellationToken ct) =>
        Task.FromResult(_idempotencyToReportId.TryGetValue(key, out var id) ? id : null);

    public Task LinkIdempotencyKeyAsync(string key, string reportId, CancellationToken ct)
    {
        _idempotencyToReportId.TryAdd(key, reportId);

        return Task.CompletedTask;
    }

    private Task Transition(
        string reportId,
        Func<TranscriptStatus, TranscriptStatus> change,
        TranscriptState allowedFrom
    )
    {
        if (!_byReportId.TryGetValue(reportId, out var current))
            throw new InvalidOperationException($"Unknown report id {reportId}.");

        if (current.State != allowedFrom)
            throw new InvalidOperationException(
                $"Cannot move {reportId} from {current.State} via this transition (expected {allowedFrom})."
            );

        _byReportId[reportId] = change(current);

        return Task.CompletedTask;
    }
}
