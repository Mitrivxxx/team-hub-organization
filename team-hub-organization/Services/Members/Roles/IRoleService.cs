using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<RoleListItemResponse>> ListAsync(Guid organizationId, Guid actorUserId, RoleScope? scope, CancellationToken cancellationToken = default);
    Task<RoleDetailResponse?> GetAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleDetailResponse> CreateAsync(Guid organizationId, CreateRoleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleDetailResponse?> UpdateAsync(Guid organizationId, Guid roleId, UpdateRoleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionSummaryDto>> ListPermissionsAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionSummaryDto>?> ReplacePermissionsAsync(Guid organizationId, Guid roleId, AssignRolePermissionsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PermissionSummaryDto>?> AddPermissionsAsync(Guid organizationId, Guid roleId, AssignRolePermissionsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task RemovePermissionAsync(Guid organizationId, Guid roleId, Guid permissionId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSummaryDto>> ListMembersAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<MemberSummaryDto> AssignMemberAsync(Guid organizationId, Guid roleId, AssignRoleMemberRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task RevokeMemberAsync(Guid organizationId, Guid roleId, Guid userId, Guid actorUserId, CancellationToken cancellationToken = default);
}
