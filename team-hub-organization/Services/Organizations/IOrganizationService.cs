using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Organizations;

public interface IOrganizationService
{
    Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetByIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetBySlugAsync(string slug, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> UpdateStatusAsync(Guid organizationId, UpdateOrganizationStatusRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse> RestoreAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task TransferOwnershipAsync(Guid organizationId, Guid actorUserId, Guid newOwnerUserId, CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class OrganizationAccessException : Exception
{
    public OrganizationAccessException(string message) : base(message) { }
}

public sealed class OrganizationConflictException : Exception
{
    public OrganizationConflictException(string message) : base(message) { }
}

public sealed class OrganizationNotFoundException : Exception
{
    public OrganizationNotFoundException(string message = "Organization was not found.") : base(message) { }
}

public sealed class OrganizationValidationException : Exception
{
    public OrganizationValidationException(string message) : base(message) { }
}

public sealed class OrganizationGoneException : Exception
{
    public OrganizationGoneException(string message) : base(message) { }
}
