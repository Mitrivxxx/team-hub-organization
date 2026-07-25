using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.Roles;

public sealed class RoleService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz) : IRoleService
{
    public async Task<IReadOnlyList<RoleResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        RoleScope? scope,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var query = db.Roles.AsNoTracking().Where(r => r.OrganizationId == organizationId);
        if (scope is not null)
            query = query.Where(r => r.Scope == scope);

        var roles = await query.OrderBy(r => r.Scope).ThenBy(r => r.Name).ToListAsync(cancellationToken);
        return await MapRolesAsync(roles, cancellationToken);
    }

    public async Task<RoleResponse?> GetAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken);

        if (role is null)
            return null;

        return (await MapRolesAsync([role], cancellationToken))[0];
    }

    public async Task<RoleResponse> CreateAsync(
        Guid organizationId,
        CreateRoleRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        if (!TryParseScope(request.Scope, out var scope))
            throw new OrganizationValidationException("Scope must be ORG or TEAM.");

        var name = request.Name.Trim();
        if (IsSystemName(name, scope))
            throw new OrganizationValidationException("Cannot create a role with a reserved system name.");

        if (await db.Roles.AnyAsync(
                r => r.OrganizationId == organizationId && r.Scope == scope && r.Name == name,
                cancellationToken))
            throw new OrganizationConflictException("A role with this name already exists for the scope.");

        var role = new Role
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name,
            Scope = scope,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(organizationId, role.Id, actorUserId, cancellationToken))!;
    }

    public async Task<RoleResponse?> UpdateAsync(
        Guid organizationId,
        Guid roleId,
        UpdateRoleRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        var role = await db.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken);

        if (role is null)
            return null;

        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org)
            throw new OrganizationValidationException("Cannot rename the Owner role.");

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var name = request.Name.Trim();
            if (IsSystemName(name, role.Scope) && !string.Equals(name, role.Name, StringComparison.Ordinal))
                throw new OrganizationValidationException("Cannot rename a role to a reserved system name.");

            if (await db.Roles.AnyAsync(
                    r => r.OrganizationId == organizationId
                         && r.Scope == role.Scope
                         && r.Name == name
                         && r.Id != roleId,
                    cancellationToken))
                throw new OrganizationConflictException("A role with this name already exists for the scope.");

            role.Name = name;
            await db.SaveChangesAsync(cancellationToken);
        }

        return await GetAsync(organizationId, roleId, actorUserId, cancellationToken);
    }

    public async Task DeleteAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);

        var role = await db.Roles
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        if (IsSystemName(role.Name, role.Scope))
            throw new OrganizationConflictException("System roles cannot be deleted.");

        var inUse = await db.OrganizationMembers.AnyAsync(m => m.RoleId == roleId, cancellationToken)
                    || await db.TeamMembers.AnyAsync(m => m.RoleId == roleId, cancellationToken)
                    || await db.Invitations.AnyAsync(i => i.OrgRoleId == roleId || i.TeamRoleId == roleId, cancellationToken);

        if (inUse)
            throw new OrganizationConflictException("Role is in use and cannot be deleted.");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<RoleResponse?> ReplacePermissionsAsync(
        Guid organizationId,
        Guid roleId,
        RolePermissionsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken);
        if (role is null)
            return null;

        EnsureCanEditPermissions(role);

        var permissions = await ResolvePermissionsAsync(request.PermissionCodes, cancellationToken);
        var existing = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existing);
        db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { RoleId = roleId, PermissionId = p.Id }));
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(organizationId, roleId, actorUserId, cancellationToken);
    }

    public async Task<RoleResponse?> AddPermissionsAsync(
        Guid organizationId,
        Guid roleId,
        RolePermissionsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken);
        if (role is null)
            return null;

        EnsureCanEditPermissions(role);

        var permissions = await ResolvePermissionsAsync(request.PermissionCodes, cancellationToken);
        var existingIds = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();

        foreach (var permission in permissions.Where(p => !existingSet.Contains(p.Id)))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
        }

        await db.SaveChangesAsync(cancellationToken);
        return await GetAsync(organizationId, roleId, actorUserId, cancellationToken);
    }

    public async Task RemovePermissionAsync(
        Guid organizationId,
        Guid roleId,
        string permissionCode,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        EnsureCanEditPermissions(role);

        var permission = await db.Permissions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == permissionCode, cancellationToken)
            ?? throw new OrganizationNotFoundException("Permission was not found.");

        var link = await db.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permission.Id, cancellationToken);

        if (link is null)
            throw new OrganizationNotFoundException("Permission is not assigned to this role.");

        db.RolePermissions.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task<Role?> RequireRoleAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken);

    static void EnsureCanEditPermissions(Role role)
    {
        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org)
            throw new OrganizationValidationException("Cannot modify permissions on the Owner role.");
    }

    async Task<List<Permission>> ResolvePermissionsAsync(IReadOnlyList<string> codes, CancellationToken cancellationToken)
    {
        var distinct = codes.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var permissions = await db.Permissions.Where(p => distinct.Contains(p.Code)).ToListAsync(cancellationToken);
        if (permissions.Count != distinct.Count)
            throw new OrganizationValidationException("One or more permission codes are invalid.");
        return permissions;
    }

    async Task<IReadOnlyList<RoleResponse>> MapRolesAsync(IReadOnlyList<Role> roles, CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(r => r.Id).ToList();
        var permissionRows = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Join(db.Permissions.AsNoTracking(), rp => rp.PermissionId, p => p.Id, (rp, p) => new { rp.RoleId, p.Code })
            .ToListAsync(cancellationToken);

        var codesByRole = permissionRows
            .GroupBy(x => x.RoleId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Code).OrderBy(c => c).ToList());

        return roles.Select(r => new RoleResponse
        {
            Id = r.Id,
            OrganizationId = r.OrganizationId!.Value,
            Name = r.Name,
            Scope = r.Scope == RoleScope.Org ? "ORG" : "TEAM",
            CreatedAt = r.CreatedAt,
            PermissionCodes = codesByRole.GetValueOrDefault(r.Id, []),
            IsSystem = IsSystemName(r.Name, r.Scope)
        }).ToList();
    }

    static bool TryParseScope(string scope, out RoleScope parsed)
    {
        if (string.Equals(scope, "ORG", StringComparison.OrdinalIgnoreCase))
        {
            parsed = RoleScope.Org;
            return true;
        }

        if (string.Equals(scope, "TEAM", StringComparison.OrdinalIgnoreCase))
        {
            parsed = RoleScope.Team;
            return true;
        }

        parsed = default;
        return false;
    }

    static bool IsSystemName(string name, RoleScope scope) =>
        scope == RoleScope.Org
            ? SystemRoleNames.IsSystemOrgRole(name)
            : SystemRoleNames.IsSystemTeamRole(name);
}
