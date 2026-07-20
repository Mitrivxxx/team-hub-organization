namespace team_hub_organization.Models;

public class Invitation
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? TeamId { get; set; }
    public string Email { get; set; } = "";
    public Guid InvitedByUserId { get; set; }
    public string Token { get; set; } = "";
    public Guid OrgRoleId { get; set; }
    public Guid? TeamRoleId { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public Team? Team { get; set; }
    public Role OrgRole { get; set; } = null!;
    public Role? TeamRole { get; set; }
}
