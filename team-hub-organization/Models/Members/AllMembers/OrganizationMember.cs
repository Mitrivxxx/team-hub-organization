namespace team_hub_organization.Models;

public class OrganizationMember
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }

    public Organization Organization { get; set; } = null!;
    public ICollection<OrganizationMemberRole> MemberRoles { get; set; } = [];
}
