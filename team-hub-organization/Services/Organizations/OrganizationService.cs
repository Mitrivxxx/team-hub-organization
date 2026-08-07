using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamHub.BlobStorage;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Members.Audit;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Organizations;

public sealed class OrganizationService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IActivityRecorder activity,
    IAuditRecorder audit,
    IOrganizationLifecycleNotifier lifecycleNotifier,
    IOptions<OrganizationLifecycleOptions> lifecycleOptions,
    IOptions<OrganizationQuotasOptions> quotas,
    IServiceProvider serviceProvider) : IOrganizationService
{
    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();
    OrganizationLifecycleOptions Lifecycle => lifecycleOptions.Value;

    public async Task<OrganizationResponse> CreateAsync(CreateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var ownedOrgCount = await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Join(db.Roles.AsNoTracking(), m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .Where(x => x.r.Name == SystemRoleNames.Owner && x.r.Scope == RoleScope.Org)
            .Join(
                db.Organizations.AsNoTracking().Where(o => o.DeletedAt == null),
                x => x.m.OrganizationId,
                o => o.Id,
                (_, _) => 1)
            .CountAsync(cancellationToken);

        if (ownedOrgCount >= quotas.Value.MaxOrgsPerUser)
            throw new OrganizationQuotaExceededException(
                $"Organization quota exceeded (max {quotas.Value.MaxOrgsPerUser} organizations per user).");

        var now = DateTimeOffset.UtcNow;
        string slug;

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            var baseSlug = SlugHelper.GenerateFromName(request.Name);
            slug = await SlugHelper.EnsureUniqueSlugAsync(
                candidate => db.Organizations.AnyAsync(o => o.Slug == candidate && o.DeletedAt == null, cancellationToken),
                baseSlug,
                cancellationToken);
        }
        else
        {
            slug = request.Slug.Trim().ToLowerInvariant();
            if (await db.Organizations.AnyAsync(o => o.Slug == slug && o.DeletedAt == null, cancellationToken))
                throw new OrganizationConflictException("Organization slug is already taken.");
        }

        var email = await OrganizationEmailHelper.GenerateUniqueEmailAsync(
            candidate => db.Organizations.AnyAsync(o => o.Email == candidate && o.DeletedAt == null, cancellationToken),
            request.Name,
            cancellationToken);

        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Nip = request.Nip.Trim(),
            Email = email,
            Address = new OrganizationAddress
            {
                Country = request.Address.Country.Trim(),
                City = request.Address.City.Trim(),
                PostalCode = request.Address.PostalCode.Trim()
            },
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Organizations.Add(organization);

        var seeded = await OrganizationRoleSeeder.SeedSystemRolesAsync(db, organization.Id, now, cancellationToken);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = userId,
            JoinedAt = now
        });

        db.OrganizationMemberRoles.Add(new OrganizationMemberRole
        {
            OrganizationId = organization.Id,
            UserId = userId,
            RoleId = seeded.Owner.Id,
            AssignedAt = now
        });

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

        await authz.EnsureMemberAsync(organizationId, userId, cancellationToken);
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

        await authz.EnsureMemberAsync(organization.Id, userId, cancellationToken);
        return ToResponse(organization);
    }

    public async Task<OrganizationResponse?> UpdateAsync(Guid organizationId, UpdateOrganizationRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        await authz.EnsurePermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgManage, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Name))
            organization.Name = request.Name.Trim();

        if (request.Description is not null)
            organization.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        if (request.Nip is not null)
            organization.Nip = request.Nip.Trim();

        if (request.Address is not null)
        {
            organization.Address.Country = request.Address.Country.Trim();
            organization.Address.City = request.Address.City.Trim();
            organization.Address.PostalCode = request.Address.PostalCode.Trim();
        }

        organization.UpdatedAt = DateTimeOffset.UtcNow;
        activity.Record(
            organizationId,
            ActivityTypes.OrganizationUpdated,
            userId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            details: new { name = organization.Name });
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(organization);
    }

    public async Task<OrganizationResponse?> UpdateStatusAsync(
        Guid organizationId,
        UpdateOrganizationStatusRequest request,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return null;

        await authz.EnsurePermissionAsync(
            organizationId,
            userId,
            OrganizationPermissionCodes.OrgManage,
            cancellationToken,
            requireMutable: false);

        if (!TryParseStatus(request.Status, out var newStatus))
            throw new OrganizationValidationException("Status must be active, suspended, or archived.");

        if (organization.Status == newStatus)
            return ToResponse(organization);

        var previous = organization.Status;
        organization.Status = newStatus;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        activity.Record(
            organizationId,
            ActivityTypes.OrganizationStatusChanged,
            userId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            details: new { from = previous.ToString().ToLowerInvariant(), to = newStatus.ToString().ToLowerInvariant() });
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(organization);
    }

    public async Task<bool> SoftDeleteAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

        if (organization is null)
            return false;

        await authz.EnsurePermissionAsync(
            organizationId,
            userId,
            OrganizationPermissionCodes.OrgDelete,
            cancellationToken,
            requireMutable: false);

        var exportCutoff = DateTimeOffset.UtcNow.AddDays(-Lifecycle.RetentionDays);
        var hasRecentExport = await db.ImportExportJobs.AsNoTracking()
            .AnyAsync(
                j => j.OrganizationId == organizationId
                     && j.Type == ImportExportJobType.Export
                     && j.Status == ImportExportJobStatus.Completed
                     && j.CompletedAt != null
                     && j.CompletedAt >= exportCutoff,
                cancellationToken);

        if (!hasRecentExport)
            throw new OrganizationConflictException(
                $"A completed export within the last {Lifecycle.RetentionDays} days is required before delete.");

        var memberUserIds = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        organization.DeletedAt = now;
        organization.Status = OrganizationStatus.Archived;
        organization.UpdatedAt = now;
        activity.Record(
            organizationId,
            ActivityTypes.OrganizationDeleted,
            userId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            details: new { name = organization.Name });
        audit.Record(
            organizationId,
            AuditActions.OrganizationDeleted,
            userId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            details: new { name = organization.Name });
        await db.SaveChangesAsync(cancellationToken);

        await lifecycleNotifier.NotifyClosingAsync(organizationId, memberUserIds, cancellationToken);

        return true;
    }

    public async Task<OrganizationResponse> RestoreAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.DeletedAt != null, cancellationToken)
            ?? throw new OrganizationNotFoundException();

        if (!await authz.IsMemberAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("User is not a member of this organization.");

        if (!await authz.HasPermissionAsync(organizationId, userId, OrganizationPermissionCodes.OrgDelete, cancellationToken))
            throw new OrganizationAccessException($"Missing permission '{OrganizationPermissionCodes.OrgDelete}'.");

        var deletedAt = organization.DeletedAt!.Value;
        var deadline = deletedAt.AddDays(Lifecycle.RetentionDays);
        if (DateTimeOffset.UtcNow > deadline)
            throw new OrganizationGoneException("Restore window has expired; organization is pending purge.");

        if (await db.Organizations.AnyAsync(
                o => o.Id != organizationId && o.Slug == organization.Slug && o.DeletedAt == null,
                cancellationToken))
            throw new OrganizationConflictException("Organization slug is already taken.");

        if (!string.IsNullOrEmpty(organization.Email)
            && await db.Organizations.AnyAsync(
                o => o.Id != organizationId && o.Email == organization.Email && o.DeletedAt == null,
                cancellationToken))
            throw new OrganizationConflictException("Organization email is already taken.");

        organization.DeletedAt = null;
        organization.Status = OrganizationStatus.Active;
        organization.UpdatedAt = DateTimeOffset.UtcNow;
        activity.Record(
            organizationId,
            ActivityTypes.OrganizationRestored,
            userId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            details: new { name = organization.Name });
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(organization);
    }

    public async Task TransferOwnershipAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid newOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == newOwnerUserId)
            throw new OrganizationValidationException("Cannot transfer ownership to yourself.");

        await authz.EnsureOwnerAsync(organizationId, actorUserId, cancellationToken);

        var ownerRole = await db.Roles
            .FirstOrDefaultAsync(
                r => r.OrganizationId == organizationId
                     && r.Name == SystemRoleNames.Owner
                     && r.Scope == RoleScope.Org,
                cancellationToken)
            ?? throw new OrganizationValidationException("Owner role was not found.");

        var adminRole = await db.Roles
            .FirstOrDefaultAsync(
                r => r.OrganizationId == organizationId
                     && r.Name == SystemRoleNames.Admin
                     && r.Scope == RoleScope.Org,
                cancellationToken)
            ?? throw new OrganizationValidationException("Admin role was not found.");

        _ = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == actorUserId, cancellationToken)
            ?? throw new OrganizationAccessException("User is not a member of this organization.");

        _ = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == newOwnerUserId, cancellationToken)
            ?? throw new OrganizationValidationException("New owner must already be an organization member.");

        var now = DateTimeOffset.UtcNow;

        var actorOwner = await db.OrganizationMemberRoles
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == actorUserId && m.RoleId == ownerRole.Id,
                cancellationToken);

        if (actorOwner is not null)
            db.OrganizationMemberRoles.Remove(actorOwner);

        if (!await db.OrganizationMemberRoles.AnyAsync(
                m => m.OrganizationId == organizationId && m.UserId == actorUserId && m.RoleId == adminRole.Id,
                cancellationToken))
        {
            db.OrganizationMemberRoles.Add(new OrganizationMemberRole
            {
                OrganizationId = organizationId,
                UserId = actorUserId,
                RoleId = adminRole.Id,
                AssignedAt = now
            });
        }

        var targetOwner = await db.OrganizationMemberRoles
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == newOwnerUserId && m.RoleId == ownerRole.Id,
                cancellationToken);

        if (targetOwner is null)
        {
            db.OrganizationMemberRoles.Add(new OrganizationMemberRole
            {
                OrganizationId = organizationId,
                UserId = newOwnerUserId,
                RoleId = ownerRole.Id,
                AssignedAt = now
            });
        }

        activity.Record(
            organizationId,
            ActivityTypes.OwnershipTransferred,
            actorUserId,
            targetUserId: newOwnerUserId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            occurredAt: now);
        audit.Record(
            organizationId,
            AuditActions.OwnershipTransferred,
            actorUserId,
            targetUserId: newOwnerUserId,
            entityType: ActivityEntityTypes.Organization,
            entityId: organizationId,
            occurredAt: now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task LeaveAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, userId, cancellationToken);
        await authz.EnsureMutableAsync(organizationId, cancellationToken);

        if (await authz.IsOwnerAsync(organizationId, userId, cancellationToken))
        {
            var ownerCount = await db.OrganizationMemberRoles
                .Join(db.Roles, m => m.RoleId, r => r.Id, (m, r) => new { m, r })
                .CountAsync(
                    x => x.m.OrganizationId == organizationId
                         && x.r.Name == SystemRoleNames.Owner
                         && x.r.Scope == RoleScope.Org,
                    cancellationToken);

            if (ownerCount <= 1)
                throw new OrganizationConflictException("Owner cannot leave without transferring ownership first.");
        }

        var member = await db.OrganizationMembers
            .FirstAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        var roleAssignments = await db.OrganizationMemberRoles
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .ToListAsync(cancellationToken);

        var teamMemberships = await db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Join(
                db.Teams.Where(t => t.OrganizationId == organizationId),
                tm => tm.TeamId,
                t => t.Id,
                (tm, _) => tm)
            .ToListAsync(cancellationToken);

        db.OrganizationMemberRoles.RemoveRange(roleAssignments);
        db.TeamMembers.RemoveRange(teamMemberships);
        db.OrganizationMembers.Remove(member);
        activity.Record(
            organizationId,
            ActivityTypes.MemberLeft,
            userId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Member,
            entityId: userId,
            details: new { reason = "left" });
        await db.SaveChangesAsync(cancellationToken);
    }

    OrganizationResponse ToResponse(Organization organization) => new()
    {
        Id = organization.Id,
        Name = organization.Name,
        Slug = organization.Slug,
        Description = organization.Description,
        Nip = organization.Nip,
        Email = organization.Email,
        Address = new OrganizationAddressDto
        {
            Country = organization.Address.Country,
            City = organization.Address.City,
            PostalCode = organization.Address.PostalCode
        },
        AvatarUrl = OrganizationAvatarService.ResolveAvatarUrl(organization.AvatarUrl, BlobStorage),
        Status = organization.Status.ToString().ToLowerInvariant(),
        CreatedAt = organization.CreatedAt,
        UpdatedAt = organization.UpdatedAt
    };

    static bool TryParseStatus(string raw, out OrganizationStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return Enum.TryParse(raw.Trim(), ignoreCase: true, out status)
               && Enum.IsDefined(status);
    }

    static bool IsSlugConflict(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("IX_organizations_Slug", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("organizations", StringComparison.OrdinalIgnoreCase) == true
           && ex.InnerException.Message.Contains("Slug", StringComparison.OrdinalIgnoreCase);
}
