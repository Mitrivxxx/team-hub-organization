namespace team_hub_organization.Dtos;

public sealed class DemoSeedContextResponse
{
    public Guid OrganizationId { get; init; }
    public string Slug { get; init; } = "";
    public string Name { get; init; } = "";
    public Guid OwnerUserId { get; init; }
    public DemoSeedRolesContext Roles { get; init; } = new();
    public IReadOnlyList<DemoSeedTeamContext> Teams { get; init; } = [];
    public IReadOnlyList<Guid> SampleMemberUserIds { get; init; } = [];
    /// <summary>User not yet in the org (demo00021+ from auth) for POST /members.</summary>
    public Guid? AddMemberUserId { get; init; }
    public string? AddMemberUsername { get; init; }
    public IReadOnlyList<DemoSeedPermissionContext> Permissions { get; init; } = [];
    public IReadOnlyList<DemoSeedInvitationContext> PendingInvitations { get; init; } = [];
}

public sealed class DemoSeedRolesContext
{
    public Guid OrgOwner { get; init; }
    public Guid OrgAdmin { get; init; }
    public Guid OrgMember { get; init; }
    public Guid TeamLead { get; init; }
    public Guid TeamMember { get; init; }
}

public sealed class DemoSeedTeamContext
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
}

public sealed class DemoSeedPermissionContext
{
    public Guid Id { get; init; }
    public string Code { get; init; } = "";
}

public sealed class DemoSeedInvitationContext
{
    public Guid Id { get; init; }
    public string Email { get; init; } = "";
}
