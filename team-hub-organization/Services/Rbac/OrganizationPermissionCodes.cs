namespace team_hub_organization.Services.Rbac;

public static class OrganizationPermissionCodes
{
    public const string OrgManage = "org.manage";
    public const string OrgMembersManage = "org.members.manage";
    public const string OrgTeamsManage = "org.teams.manage";
    public const string OrgRolesManage = "org.roles.manage";
    public const string OrgDelete = "org.delete";
    public const string TeamManage = "team.manage";
    public const string TeamMembersManage = "team.members.manage";

    public static readonly IReadOnlyList<(string Code, string Name, string Description)> Catalog =
    [
        (OrgManage, "Manage organization", "Update organization settings and avatar"),
        (OrgMembersManage, "Manage members", "Manage organization members and invitations"),
        (OrgTeamsManage, "Manage teams", "Manage teams and team membership"),
        (OrgRolesManage, "Manage roles", "Manage roles and role permissions"),
        (OrgDelete, "Delete organization", "Delete the organization"),
        (TeamManage, "Manage team", "Update team settings and avatar"),
        (TeamMembersManage, "Manage team members", "Manage team members")
    ];

    public static readonly IReadOnlyList<string> All =
    [
        OrgManage,
        OrgMembersManage,
        OrgTeamsManage,
        OrgRolesManage,
        OrgDelete,
        TeamManage,
        TeamMembersManage
    ];

    public static readonly IReadOnlyList<string> AdminDefaults =
    [
        OrgManage,
        OrgMembersManage,
        OrgTeamsManage,
        OrgRolesManage,
        TeamManage,
        TeamMembersManage
    ];

    public static bool IsSystemCode(string code) =>
        All.Contains(code, StringComparer.Ordinal);
}
