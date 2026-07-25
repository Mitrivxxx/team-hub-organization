namespace team_hub_organization.Dtos;

public sealed class RoleSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Scope { get; set; } = "";
    public bool IsSystem { get; set; }
}

public sealed class PermissionSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public bool IsSystem { get; set; }
}

public sealed class MemberSummaryDto
{
    public Guid UserId { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class MemberResponse
{
    public Guid UserId { get; set; }
    public IReadOnlyList<RoleSummaryDto> Roles { get; set; } = [];
    public DateTimeOffset JoinedAt { get; set; }
    public IReadOnlyList<Guid> TeamIds { get; set; } = [];
}

public sealed class AddMemberRequest
{
    public Guid UserId { get; set; }
    public IReadOnlyList<Guid> RoleIds { get; set; } = [];
}

public sealed class UpdateMemberRequest
{
    public IReadOnlyList<Guid> RoleIds { get; set; } = [];
}

public sealed class TransferOwnershipRequest
{
    public Guid NewOwnerUserId { get; set; }
}

public sealed class TeamMembershipResponse
{
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = "";
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? JobTitle { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class PermissionListItemResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int RoleCount { get; set; }
}

public sealed class PermissionDetailResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int RoleCount { get; set; }
    public IReadOnlyList<RoleSummaryDto> Roles { get; set; } = [];
}

public sealed class CreatePermissionRequest
{
    public string Name { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class UpdatePermissionRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class RoleListItemResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Scope { get; set; } = "";
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public int PermissionCount { get; set; }
}

public sealed class RoleDetailResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Scope { get; set; } = "";
    public bool IsSystem { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int MemberCount { get; set; }
    public IReadOnlyList<MemberSummaryDto> Members { get; set; } = [];
    public IReadOnlyList<PermissionSummaryDto> Permissions { get; set; } = [];
}

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string Scope { get; set; } = "";
}

public sealed class UpdateRoleRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class AssignRolePermissionsRequest
{
    public IReadOnlyList<Guid> PermissionIds { get; set; } = [];
}

public sealed class AssignRoleMemberRequest
{
    public Guid UserId { get; set; }
}

public sealed class TeamResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? AvatarUrl { get; set; }
    public int MemberCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CreateTeamRequest
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class UpdateTeamRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class TeamMemberResponse
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string RoleName { get; set; } = "";
    public string? JobTitle { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
}

public sealed class AddTeamMemberRequest
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }
    public string? JobTitle { get; set; }
}

public sealed class UpdateTeamMemberRequest
{
    public Guid? RoleId { get; set; }
    public string? JobTitle { get; set; }
}

public sealed class InvitationResponse
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? TeamId { get; set; }
    public string Email { get; set; } = "";
    public Guid InvitedByUserId { get; set; }
    public IReadOnlyList<Guid> OrgRoleIds { get; set; } = [];
    public Guid? TeamRoleId { get; set; }
    public string Status { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? Token { get; set; }
}

public sealed class CreateInvitationRequest
{
    public string Email { get; set; } = "";
    public IReadOnlyList<Guid> OrgRoleIds { get; set; } = [];
    public Guid? TeamId { get; set; }
    public Guid? TeamRoleId { get; set; }
}

public sealed class MeResponse
{
    public Guid UserId { get; set; }
    public IReadOnlyList<RoleSummaryDto> Roles { get; set; } = [];
    public DateTimeOffset JoinedAt { get; set; }
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public IReadOnlyList<TeamMembershipResponse> Teams { get; set; } = [];
}
