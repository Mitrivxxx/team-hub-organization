using Microsoft.EntityFrameworkCore;
using TeamHub.BlobStorage;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationService(OrganizationDbContext db, IServiceProvider serviceProvider) : IOrganizationService
{
    public const string OwnerRoleName = "Owner";

    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        string slug;

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            var baseSlug = SlugHelper.GenerateFromName(request.Name);
            slug = await SlugHelper.EnsureUniqueSlugAsync(
                candidate => db.Organizations.AnyAsync(o => o.Slug == candidate, cancellationToken),
                baseSlug,
                cancellationToken);
        }
        else
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (await db.Organizations.AnyAsync(o => o.Slug == slug, cancellationToken))
                throw new OrganizationConflictException("Organization slug is already taken.");
        }

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            CreatedAt = now,
            UpdatedAt = now
        };

        var ownerRole = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = OwnerRoleName,
            Scope = RoleScope.Org,
            CreatedAt = now
        };

        var member = new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = userId,
            RoleId = ownerRole.Id,
            JoinedAt = now
        };

        db.Organizations.Add(organization);
        db.Roles.Add(ownerRole);
        db.OrganizationMembers.Add(member);

        if (db.Database.IsRelational())
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsSlugConflict(ex))
            {
                throw new OrganizationConflictException("Organization slug is already taken.");
            }
        }
        else
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsSlugConflict(ex))
            {
                throw new OrganizationConflictException("Organization slug is already taken.");
            }
        }

        return ToResponse(organization);
    }

    public async Task<IReadOnlyList<OrganizationResponse>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var organizations = await db.OrganizationMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(
                db.Organizations.AsNoTracking().Where(o => o.DeletedAt == null),
                member => member.OrganizationId,
                organization => organization.Id,
                (_, organization) => organization)
            .OrderBy(o => o.Name)
            .ToListAsync(cancellationToken);

        return organizations.Select(ToResponse).ToList();
    }

    public async Task<OrganizationResponse?> GetByIdAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        if (!await IsMemberAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("User is not a member of this organization.");

        return ToResponse(organization);
    }

    public async Task<OrganizationResponse?> GetBySlugAsync(string slug, Guid userId, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var organization = await db.Organizations
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Slug == normalizedSlug && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        if (!await IsMemberAsync(organization.Id, userId, cancellationToken))
            throw new OrganizationAccessException("User is not a member of this organization.");

        return ToResponse(organization);
    }

    public async Task<OrganizationResponse?> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        if (!await IsMemberAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("User is not a member of this organization.");

        if (!string.IsNullOrWhiteSpace(request.Name))
            organization.Name = request.Name.Trim();

        organization.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(organization);
    }

    public async Task<bool> SoftDeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return false;

        if (!await IsOwnerAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("Only organization owners can delete the organization.");

        organization.DeletedAt = DateTimeOffset.UtcNow;
        organization.UpdatedAt = organization.DeletedAt.Value;
        await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    async Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await db.OrganizationMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

    async Task<bool> IsOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken) =>
        await db.OrganizationMembers.AsNoTracking()
            .Join(
                db.Roles.AsNoTracking(),
                member => member.RoleId,
                role => role.Id,
                (member, role) => new { member, role })
            .AnyAsync(
                x => x.member.OrganizationId == organizationId
                     && x.member.UserId == userId
                     && x.role.Name == OwnerRoleName
                     && x.role.Scope == RoleScope.Org,
                cancellationToken);

    OrganizationResponse ToResponse(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Slug = organization.Slug,
        AvatarUrl = OrganizationAvatarService.ResolveAvatarUrl(organization.AvatarUrl, BlobStorage),
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt
    };

    static bool IsSlugConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_organizations_Slug", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("organizations", StringComparison.OrdinalIgnoreCase) == true
           && ex.InnerException.Message.Contains("Slug", StringComparison.OrdinalIgnoreCase);
}
