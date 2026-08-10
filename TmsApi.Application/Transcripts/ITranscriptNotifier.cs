namespace TmsApi.Application.Transcripts;

public interface ITranscriptNotifier
{
    Task TranscriptReadyAsync(
        int studentId,
        string reportId,
        string downloadUrl,
        CancellationToken ct = default
    );
}
