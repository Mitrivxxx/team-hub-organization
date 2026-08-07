using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;

namespace team_hub_organization.Services.Rbac;

public sealed class OrganizationAuthorizationService(OrganizationDbContext db) : IOrganizationAuthorizationService
{
    public Task<bool> OrganizationExistsAsync(Guid organizationId, CancellationToken cancellationToken = default) =>
        db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Id == organizationId && o.DeletedAt == null, cancellationToken);

    public Task<bool> IsMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        db.OrganizationMembers.AsNoTracking()
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

    public async Task EnsureMemberAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await OrganizationExistsAsync(organizationId, cancellationToken))
            throw new OrganizationNotFoundException();

        if (!await IsMemberAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("User is not a member of this organization.");
    }

    public async Task EnsureMutableAsync(Guid organizationId, CancellationToken cancellationToken = default)
    {
        var status = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId && o.DeletedAt == null)
            .Select(o => (OrganizationStatus?)o.Status)
            .FirstOrDefaultAsync(cancellationToken);

        if (status is null)
            throw new OrganizationNotFoundException();

        if (status is OrganizationStatus.Suspended or OrganizationStatus.Archived)
            throw new OrganizationConflictException("Organization is read-only in the current status.");
    }

    public async Task EnsurePermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default,
        bool requireMutable = true)
    {
        await EnsureMemberAsync(organizationId, userId, cancellationToken);

        if (!await HasPermissionAsync(organizationId, userId, permissionCode, cancellationToken))
            throw new OrganizationAccessException($"Missing permission '{permissionCode}'.");

        if (requireMutable)
            await EnsureMutableAsync(organizationId, cancellationToken);
    }

    public async Task EnsureOwnerAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default,
        bool requireMutable = true)
    {
        await EnsureMemberAsync(organizationId, userId, cancellationToken);

        if (!await IsOwnerAsync(organizationId, userId, cancellationToken))
            throw new OrganizationAccessException("Only organization owners can perform this action.");

        if (requireMutable)
            await EnsureMutableAsync(organizationId, cancellationToken);
    }

    public Task<bool> IsOwnerAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
        db.OrganizationMemberRoles.AsNoTracking()
            .Join(
                db.Roles.AsNoTracking(),
                assignment => assignment.RoleId,
                role => role.Id,
                (assignment, role) => new { assignment, role })
            .AnyAsync(
                x => x.assignment.OrganizationId == organizationId
                     && x.assignment.UserId == userId
                     && x.role.Name == SystemRoleNames.Owner
                     && x.role.Scope == RoleScope.Org,
                cancellationToken);

    public async Task<bool> HasPermissionAsync(
        Guid organizationId,
        Guid userId,
        string permissionCode,
        CancellationToken cancellationToken = default)
    {
        var codes = await GetPermissionCodesAsync(organizationId, userId, cancellationToken);
        return codes.Contains(permissionCode, StringComparer.Ordinal);
    }

    public async Task<IReadOnlyList<string>> GetPermissionCodesAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Join(
                db.RolePermissions.AsNoTracking(),
                assignment => assignment.RoleId,
                rp => rp.RoleId,
                (_, rp) => rp.PermissionId)
            .Join(
                db.Permissions.AsNoTracking(),
                permissionId => permissionId,
                permission => permission.Id,
                (_, permission) => permission.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }
}
