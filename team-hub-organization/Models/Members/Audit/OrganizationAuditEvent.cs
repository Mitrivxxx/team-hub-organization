namespace team_hub_organization.Models;

public sealed class OrganizationAuditEvent
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public Guid? TargetUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public string? CorrelationId { get; set; }
}
