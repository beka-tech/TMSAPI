namespace TmsApi.Application.Transcripts;

public enum TranscriptState
{
    Queued,
    Processing,
    Ready,
    Failed,
}

public record TranscriptRequest(int StudentId, string? ReportId = null)
{
    public TranscriptRequest WithReportId(string id) => this with { ReportId = id };
}

public record TranscriptStatus(
    string ReportId,
    int StudentId,
    TranscriptState State,
    DateTimeOffset RequestedAt,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? CompletedAt = null,
    string? DownloadUrl = null,
    string? ErrorMessage = null
);
