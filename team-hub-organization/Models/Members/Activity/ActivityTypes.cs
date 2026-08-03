namespace team_hub_organization.Models;

public static class ActivityTypes
{
    public const string MemberJoined = "MemberJoined";
    public const string MemberLeft = "MemberLeft";
    public const string MemberRolesChanged = "MemberRolesChanged";

    public const string InvitationSent = "InvitationSent";
    public const string InvitationAccepted = "InvitationAccepted";
    public const string InvitationRejected = "InvitationRejected";
    public const string InvitationExpired = "InvitationExpired";
    public const string InvitationResent = "InvitationResent";

    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";
    public const string RolePermissionsChanged = "RolePermissionsChanged";
    public const string RoleMemberAssigned = "RoleMemberAssigned";
    public const string RoleMemberRevoked = "RoleMemberRevoked";

    public const string PermissionCreated = "PermissionCreated";
    public const string PermissionUpdated = "PermissionUpdated";
    public const string PermissionDeleted = "PermissionDeleted";

    public const string TeamCreated = "TeamCreated";
    public const string TeamUpdated = "TeamUpdated";
    public const string TeamDeleted = "TeamDeleted";
    public const string TeamMemberAdded = "TeamMemberAdded";
    public const string TeamMemberUpdated = "TeamMemberUpdated";
    public const string TeamMemberRemoved = "TeamMemberRemoved";

    public const string OrganizationUpdated = "OrganizationUpdated";
    public const string OwnershipTransferred = "OwnershipTransferred";
    public const string OrganizationDeleted = "OrganizationDeleted";

    public const string ImportCompleted = "ImportCompleted";
    public const string ExportCompleted = "ExportCompleted";

    public static readonly IReadOnlyList<string> All =
    [
        MemberJoined, MemberLeft, MemberRolesChanged,
        InvitationSent, InvitationAccepted, InvitationRejected, InvitationExpired, InvitationResent,
        RoleCreated, RoleUpdated, RoleDeleted, RolePermissionsChanged, RoleMemberAssigned, RoleMemberRevoked,
        PermissionCreated, PermissionUpdated, PermissionDeleted,
        TeamCreated, TeamUpdated, TeamDeleted, TeamMemberAdded, TeamMemberUpdated, TeamMemberRemoved,
        OrganizationUpdated, OwnershipTransferred, OrganizationDeleted,
        ImportCompleted, ExportCompleted
    ];
}

public static class ActivityEntityTypes
{
    public const string Invitation = "Invitation";
    public const string Team = "Team";
    public const string Role = "Role";
    public const string Permission = "Permission";
    public const string Member = "Member";
    public const string Organization = "Organization";
    public const string ImportExport = "ImportExport";
}
