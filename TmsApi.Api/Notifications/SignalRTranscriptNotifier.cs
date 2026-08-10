using Microsoft.AspNetCore.SignalR;
using TmsApi.Api.Hubs;
using TmsApi.Application.Hubs;
using TmsApi.Application.Transcripts;

namespace TmsApi.Api.Notifications;

public class SignalRTranscriptNotifier(IHubContext<TmsHub, ITmsHubClient> hubContext)
    : ITranscriptNotifier
{
    public async Task TranscriptReadyAsync(
        int studentId,
        string reportId,
        string downloadUrl,
        CancellationToken ct = default
    )
    {
        await hubContext
            .Clients.Group(GroupNames.Student(studentId.ToString()))
            .ReceiveTranscriptReady(reportId, downloadUrl);
    }
}
