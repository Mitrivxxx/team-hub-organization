namespace team_hub_organization.Models;

public class OrganizationActivity
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Type { get; set; } = "";
    public Guid? ActorUserId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; }

    public Organization Organization { get; set; } = null!;
}
