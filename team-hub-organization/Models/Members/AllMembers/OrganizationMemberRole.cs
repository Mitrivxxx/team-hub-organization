namespace team_hub_organization.Models;

public class OrganizationMemberRole
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; }

    public OrganizationMember Member { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
