using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.Permissions;

public sealed class PermissionService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IActivityRecorder activity) : IPermissionService
{
    static readonly Regex CodePattern = new(@"^[a-z][a-z0-9._-]*$", RegexOptions.Compiled);

    public async Task<IReadOnlyList<PermissionListItemResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var permissions = await db.Permissions.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        var permissionIds = permissions.Select(p => p.Id).ToList();
        var counts = await db.RolePermissions.AsNoTracking()
            .Where(rp => permissionIds.Contains(rp.PermissionId))
            .GroupBy(rp => rp.PermissionId)
            .Select(g => new { PermissionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PermissionId, x => x.Count, cancellationToken);

        return permissions.Select(p => new PermissionListItemResponse
        {
            Id = p.Id,
            OrganizationId = p.OrganizationId,
            Name = p.Name,
            Code = p.Code,
            Description = p.Description,
            IsSystem = p.IsSystem,
            CreatedAt = p.CreatedAt,
            RoleCount = counts.GetValueOrDefault(p.Id)
        }).ToList();
    }

    public async Task<PermissionDetailResponse?> GetAsync(
        Guid organizationId,
        Guid permissionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var permission = await db.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == permissionId && p.OrganizationId == organizationId, cancellationToken);

        return permission is null ? null : await MapDetailAsync(permission, cancellationToken);
    }

    public async Task<PermissionDetailResponse> CreateAsync(
        Guid organizationId,
        CreatePermissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        var name = request.Name.Trim();
        var code = request.Code.Trim();

        if (string.IsNullOrWhiteSpace(name))
            throw new OrganizationValidationException("Name is required.");

        if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code))
            throw new OrganizationValidationException("Code must match pattern ^[a-z][a-z0-9._-]*$.");

        if (OrganizationPermissionCodes.IsSystemCode(code))
            throw new OrganizationValidationException("Cannot create a permission with a reserved system code.");

        if (await db.Permissions.AnyAsync(p => p.OrganizationId == organizationId && p.Code == code, cancellationToken))
            throw new OrganizationConflictException("A permission with this code already exists.");

        var permission = new Permission
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Code = code,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Permissions.Add(permission);
        activity.Record(
            organizationId,
            ActivityTypes.PermissionCreated,
            actorUserId,
            entityType: ActivityEntityTypes.Permission,
            entityId: permission.Id,
            details: new { code = permission.Code, name = permission.Name });
        await db.SaveChangesAsync(cancellationToken);

        return await MapDetailAsync(permission, cancellationToken);
    }

    public async Task<PermissionDetailResponse?> UpdateAsync(
        Guid organizationId,
        Guid permissionId,
        UpdatePermissionRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.Id == permissionId && p.OrganizationId == organizationId, cancellationToken);

        if (permission is null)
            return null;

        if (request.Name is null && request.Description is null)
            throw new OrganizationValidationException("At least one field must be provided.");

        if (permission.IsSystem)
        {
            if (request.Name is not null)
                throw new OrganizationValidationException("Cannot rename a system permission.");

            if (request.Description is not null)
                permission.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
                permission.Name = request.Name.Trim();

            if (request.Description is not null)
                permission.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        }

        activity.Record(
            organizationId,
            ActivityTypes.PermissionUpdated,
            actorUserId,
            entityType: ActivityEntityTypes.Permission,
            entityId: permission.Id,
            details: new { code = permission.Code, name = permission.Name });
        await db.SaveChangesAsync(cancellationToken);
        return await MapDetailAsync(permission, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        Guid permissionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        var permission = await db.Permissions
            .FirstOrDefaultAsync(p => p.Id == permissionId && p.OrganizationId == organizationId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Permission was not found.");

        if (permission.IsSystem)
            throw new OrganizationConflictException("System permissions cannot be deleted.");

        if (await db.RolePermissions.AnyAsync(rp => rp.PermissionId == permissionId, cancellationToken))
            throw new OrganizationConflictException("Permission is in use by one or more roles and cannot be deleted.");

        activity.Record(
            organizationId,
            ActivityTypes.PermissionDeleted,
            actorUserId,
            entityType: ActivityEntityTypes.Permission,
            entityId: permission.Id,
            details: new { code = permission.Code, name = permission.Name });
        db.Permissions.Remove(permission);
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task<PermissionDetailResponse> MapDetailAsync(Permission permission, CancellationToken cancellationToken)
    {
        var roles = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.PermissionId == permission.Id)
            .Join(
                db.Roles.AsNoTracking(),
                rp => rp.RoleId,
                r => r.Id,
                (_, r) => new RoleSummaryDto
                {
                    Id = r.Id,
                    Name = r.Name,
                    Scope = r.Scope == RoleScope.Org ? "ORG" : "TEAM",
                    IsSystem = r.IsSystem
                })
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return new PermissionDetailResponse
        {
            Id = permission.Id,
            OrganizationId = permission.OrganizationId,
            Name = permission.Name,
            Code = permission.Code,
            Description = permission.Description,
            IsSystem = permission.IsSystem,
            CreatedAt = permission.CreatedAt,
            RoleCount = roles.Count,
            Roles = roles
        };
    }
}
