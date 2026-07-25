namespace team_hub_organization.Services.Rbac;

public interface IPermissionSeedService
{
    Task EnsureCatalogAsync(CancellationToken cancellationToken = default);
}
