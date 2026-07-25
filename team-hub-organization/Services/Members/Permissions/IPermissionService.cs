using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Members.Permissions;

public interface IPermissionService
{
    Task<IReadOnlyList<PermissionResponse>> ListAsync(CancellationToken cancellationToken = default);
}
