namespace TmsApi.Domain.Entities;

public class AuditEntry
{
    public long Id { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string ActorId { get; set; } = "system";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string Changes { get; set; } = "";
}
