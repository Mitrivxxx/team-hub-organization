using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Members.Roles;

public interface IRoleService
{
    Task<IReadOnlyList<RoleResponse>> ListAsync(Guid organizationId, Guid actorUserId, RoleScope? scope, CancellationToken cancellationToken = default);
    Task<RoleResponse?> GetAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleResponse> CreateAsync(Guid organizationId, CreateRoleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleResponse?> UpdateAsync(Guid organizationId, Guid roleId, UpdateRoleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, Guid roleId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleResponse?> ReplacePermissionsAsync(Guid organizationId, Guid roleId, RolePermissionsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<RoleResponse?> AddPermissionsAsync(Guid organizationId, Guid roleId, RolePermissionsRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task RemovePermissionAsync(Guid organizationId, Guid roleId, string permissionCode, Guid actorUserId, CancellationToken cancellationToken = default);
}
