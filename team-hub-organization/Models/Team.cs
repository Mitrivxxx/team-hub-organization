namespace team_hub_organization.Models;

public class Team
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<TeamMember> Members { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
}
