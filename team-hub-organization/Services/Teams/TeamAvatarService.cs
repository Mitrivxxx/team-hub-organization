using Microsoft.EntityFrameworkCore;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Teams;

public sealed class TeamAvatarService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IServiceProvider serviceProvider) : ITeamAvatarService
{
    const long MaxFileSizeBytes = 2 * 1024 * 1024;

    static readonly Dictionary<string, string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg",
        ["image/png"] = "png",
        ["image/webp"] = "webp"
    };

    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task<TeamResponse?> UploadAsync(
        Guid organizationId,
        Guid teamId,
        IFormFile file,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();
        ValidateFile(file);

        await authz.EnsurePermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

        if (team is null)
            return null;

        var extension = AllowedContentTypes[file.ContentType];
        var blobName = BlobStoragePaths.TeamAvatar(organizationId, teamId, extension);

        if (!string.IsNullOrWhiteSpace(team.AvatarUrl)
            && !string.Equals(team.AvatarUrl, blobName, StringComparison.Ordinal))
        {
            await blobStorage.DeleteIfExistsAsync(team.AvatarUrl, cancellationToken);
        }

        await using var stream = file.OpenReadStream();
        await blobStorage.UploadAsync(blobName, stream, file.ContentType, cancellationToken);

        team.AvatarUrl = blobName;
        team.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.TeamMembers.CountAsync(tm => tm.TeamId == teamId, cancellationToken);
        return ToResponse(team, count, blobStorage);
    }

    public async Task<TeamResponse?> DeleteAsync(
        Guid organizationId,
        Guid teamId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var blobStorage = BlobStorage ?? throw new OrganizationAvatarStorageUnavailableException();

        await authz.EnsurePermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

        if (team is null)
            return null;

        if (!string.IsNullOrWhiteSpace(team.AvatarUrl))
        {
            await blobStorage.DeleteIfExistsAsync(team.AvatarUrl, cancellationToken);
            team.AvatarUrl = null;
            team.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        var count = await db.TeamMembers.CountAsync(tm => tm.TeamId == teamId, cancellationToken);
        return ToResponse(team, count, blobStorage);
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

    static TeamResponse ToResponse(Team team, int memberCount, IBlobStorageService blobStorage) => new()
    {
        Id = team.Id,
        OrganizationId = team.OrganizationId,
        Name = team.Name,
        Description = team.Description,
        AvatarUrl = BlobStoragePaths.IsTeamAvatarPath(team.AvatarUrl)
            ? blobStorage.GetReadSasUri(team.AvatarUrl!)?.ToString()
            : null,
        MemberCount = memberCount,
        CreatedAt = team.CreatedAt,
        UpdatedAt = team.UpdatedAt
    };
}
