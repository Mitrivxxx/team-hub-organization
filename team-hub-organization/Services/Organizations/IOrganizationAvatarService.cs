using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Organizations;

public interface IOrganizationAvatarService
{
    Task<OrganizationResponse?> UploadAsync(
        Guid organizationId,
        IFormFile file,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OrganizationResponse?> DeleteAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
