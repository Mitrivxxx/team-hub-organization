namespace team_hub_organization.Services.Rbac;

public interface IOrganizationAuthorizationService
{
    Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task EnsureMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task EnsurePermissionAsync(Guid organizationId, Guid userId, string permissionCode, CancellationToken cancellationToken = default);
    Task EnsureOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasPermissionAsync(Guid organizationId, Guid userId, string permissionCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetPermissionCodesAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}
