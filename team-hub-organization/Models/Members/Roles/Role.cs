namespace team_hub_organization.Models;

public class Role
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public RoleScope Scope { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<OrganizationMemberRole> OrganizationMemberRoles { get; set; } = [];
    public ICollection<TeamMember> TeamMembers { get; set; } = [];
    public ICollection<InvitationOrgRole> InvitationOrgRoles { get; set; } = [];
    public ICollection<Invitation> TeamInvitations { get; set; } = [];
}
