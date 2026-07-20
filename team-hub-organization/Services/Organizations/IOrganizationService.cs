using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Organizations;

public interface IOrganizationService
{
    Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrganizationResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetByIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetBySlugAsync(string slug, Guid userId, CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> SoftDeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class OrganizationAccessException : Exception
{
    public OrganizationAccessException(string message) : base(message) { }
}

public sealed class OrganizationConflictException : Exception
{
    public OrganizationConflictException(string message) : base(message) { }
}
