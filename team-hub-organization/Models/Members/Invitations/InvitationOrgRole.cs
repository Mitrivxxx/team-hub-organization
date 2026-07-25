namespace team_hub_organization.Models;

public class InvitationOrgRole
{
    public Guid InvitationId { get; set; }
    public Guid RoleId { get; set; }

    public Invitation Invitation { get; set; } = null!;
    public Role Role { get; set; } = null!;
}
