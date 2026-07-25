using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Members.Permissions;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionListItemResponse>> ListAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<PermissionDetailResponse?> GetAsync(Guid organizationId, Guid permissionId, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<PermissionDetailResponse> CreateAsync(Guid organizationId, CreatePermissionRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task<PermissionDetailResponse?> UpdateAsync(Guid organizationId, Guid permissionId, UpdatePermissionRequest request, Guid actorUserId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid organizationId, Guid permissionId, Guid actorUserId, CancellationToken cancellationToken = default);
}
