using Microsoft.EntityFrameworkCore;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationAvatarService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IServiceProvider serviceProvider) : IOrganizationAvatarService
{
    const long MaxFileSizeBytes = 2 * 1024 * 1024;

    static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task<OrganizationResponse?> UploadAsync(
        Guid organizationId,
        IFormFile file,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        ValidateFile(file);

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        await authz.EnsurePermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgManage, cancellationToken);

        var extension = AllowedContentTypes[file.ContentType];
        var blobName = BlobStoragePaths.OrganizationAvatar(organizationId, extension);

        if (!string.IsNullOrWhiteSpace(organization.AvatarUrl)
            && !string.Equals(organization.AvatarUrl, blobName, StringComparison.Ordinal))
        {
            await blobStorage.DeleteIfExistsAsync(organization.AvatarUrl, cancellationToken);
        }

        await using var stream = file.OpenReadStream();
        await blobStorage.UploadAsync(blobName, stream, file.ContentType, cancellationToken);

        organization.AvatarUrl = blobName;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(organization, blobStorage);
    }

    public async Task<OrganizationResponse?> DeleteAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        await authz.EnsurePermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgManage, cancellationToken);

        if (!string.IsNullOrWhiteSpace(organization.AvatarUrl))
        {
            await blobStorage.DeleteIfExistsAsync(organization.AvatarUrl, cancellationToken);
            organization.AvatarUrl = null;
            organization.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToResponse(organization, blobStorage);
    }

    static void ValidateFile(IFormFile file)
    {
        if (file.Length == 0)
            throw new OrganizationAvatarValidationException("Avatar file is required.");

        if (file.Length > MaxFileSizeBytes)
            throw new OrganizationAvatarValidationException("Avatar file must be 2 MB or smaller.");

        if (!AllowedContentTypes.ContainsKey(file.ContentType))
            throw new OrganizationAvatarValidationException("Avatar must be a JPEG, PNG, or WebP image.");
    }

    static OrganizationResponse ToResponse(Organization organization, IBlobStorageService blobStorage) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Slug = organization.Slug,
        Description = organization.Description,
        AvatarUrl = ResolveAvatarUrl(organization.AvatarUrl, blobStorage),
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt
    };

    internal static string? ResolveAvatarUrl(string? blobPath, IBlobStorageService? blobStorage)
    {
        if (string.IsNullOrWhiteSpace(blobPath) || !BlobStoragePaths.IsOrganizationAvatarPath(blobPath))
            return null;

        return blobStorage?.GetReadSasUri(blobPath)?.ToString();
    }
}
