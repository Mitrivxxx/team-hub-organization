namespace team_hub_organization.Models;

public class TeamMember
{
    public Guid TeamId { get; set; }
    public Guid UserId { get; set; }
    public string? JobTitle { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Team Team { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
