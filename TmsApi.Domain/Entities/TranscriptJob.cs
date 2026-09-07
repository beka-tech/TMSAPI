namespace TmsApi.Domain.Entities;

public class TranscriptJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int StudentId { get; set; }
    public string RequestedBy { get; set; } = "";
    public string? IdempotencyKey { get; set; }
    public string State { get; set; } = "Queued";
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Content { get; set; }
    public string? ErrorMessage { get; set; }
}
