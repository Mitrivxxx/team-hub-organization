namespace team_hub_organization.Services.Organizations;

public interface IOrganizationLifecycleNotifier
{
    Task NotifyClosingAsync(Guid organizationId, IReadOnlyList<Guid> memberUserIds, CancellationToken cancellationToken = default);
}

public sealed class NoOpOrganizationLifecycleNotifier : IOrganizationLifecycleNotifier
{
    public Task NotifyClosingAsync(
        Guid organizationId,
        IReadOnlyList<Guid> memberUserIds,
        CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
