namespace team_hub_organization.Services.Rbac;

public sealed class PermissionSeedService : IPermissionSeedService
{
    /// <summary>
    /// System permission templates are cloned per organization on create.
    /// Kept for startup compatibility; no global catalog rows are written.
    /// </summary>
    public Task EnsureCatalogAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
