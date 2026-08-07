namespace team_hub_organization.Models;

public static class AuditActions
{
    public const string OwnershipTransferred = "OwnershipTransferred";
    public const string OrganizationDeleted = "OrganizationDeleted";

    public const string RoleCreated = "RoleCreated";
    public const string RoleUpdated = "RoleUpdated";
    public const string RoleDeleted = "RoleDeleted";
    public const string RolePermissionsChanged = "RolePermissionsChanged";
    public const string RoleMemberAssigned = "RoleMemberAssigned";
    public const string RoleMemberRevoked = "RoleMemberRevoked";
    public const string MemberRolesChanged = "MemberRolesChanged";

    public const string InvitationAccepted = "InvitationAccepted";
}
