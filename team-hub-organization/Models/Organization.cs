namespace team_hub_organization.Models;

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Role> Roles { get; set; } = [];
    public ICollection<OrganizationMember> Members { get; set; } = [];
    public ICollection<Team> Teams { get; set; } = [];
    public ICollection<Invitation> Invitations { get; set; } = [];
}
