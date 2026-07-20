namespace team_hub_organization.Models;

public class Role
{
    public Guid Id { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public RoleScope Scope { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<OrganizationMember> OrganizationMembers { get; set; } = [];
    public ICollection<TeamMember> TeamMembers { get; set; } = [];
    public ICollection<Invitation> OrganizationInvitations { get; set; } = [];
    public ICollection<Invitation> TeamInvitations { get; set; } = [];
}
