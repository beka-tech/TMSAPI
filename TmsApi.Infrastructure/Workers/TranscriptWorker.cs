using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TmsApi.Application.Transcripts;
using TmsApi.Infrastructure.Transcripts;

// using Microsoft.AspNetCore.SignalR;
// using TmsApi.Appl.Hubs;

namespace TmsApi.Infrastructure.Workers;

public class TranscriptWorker(
    Channel<TranscriptRequest> channel,
    IServiceScopeFactory scopeFactory,
    ITranscriptStatusStore statusStore,
    ITranscriptNotifier notifier,
    ILogger<TranscriptWorker> logger
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        logger.LogInformation("Transcript worker started.");

        await foreach (var request in channel.Reader.ReadAllAsync(ct))
        {
            var reportId = request.ReportId!;

            try
            {
                await statusStore.MarkProcessingAsync(reportId, ct);

                await Task.Delay(TimeSpan.FromSeconds(5), ct);

                var downloadUrl = $"/api/v2/transcripts/{reportId}/download";

                await statusStore.MarkReadyAsync(reportId, downloadUrl, ct);

                await notifier.TranscriptReadyAsync(request.StudentId, reportId, downloadUrl, ct);

                logger.LogInformation("Transcript ready: {ReportId}", reportId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transcript generation failed: {ReportId}", reportId);

                await statusStore.MarkFailedAsync(reportId, ex.Message, CancellationToken.None);
            }
        }
    }
}
