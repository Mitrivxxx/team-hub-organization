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
    public async Task<IReadOnlyList<RoleListItemResponse>> ListAsync(
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
        return await MapListAsync(roles, cancellationToken);
    }

    public async Task<RoleDetailResponse?> GetAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken);

        return role is null ? null : await MapDetailAsync(role, cancellationToken);
    }

    public async Task<RoleDetailResponse> CreateAsync(
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
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Scope = scope,
            IsSystem = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(organizationId, role.Id, actorUserId, cancellationToken))!;
    }

    public async Task<RoleDetailResponse?> UpdateAsync(
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

        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org && !string.IsNullOrWhiteSpace(request.Name))
            throw new OrganizationValidationException("Cannot rename the Owner role.");

        if (request.Name is null && request.Description is null)
            throw new OrganizationValidationException("At least one field must be provided.");

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
        }

        if (request.Description is not null)
            role.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await db.SaveChangesAsync(cancellationToken);
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

        if (role.IsSystem || IsSystemName(role.Name, role.Scope))
            throw new OrganizationConflictException("System roles cannot be deleted.");

        var inUse = await db.OrganizationMemberRoles.AnyAsync(m => m.RoleId == roleId, cancellationToken)
                    || await db.TeamMembers.AnyAsync(m => m.RoleId == roleId, cancellationToken)
                    || await db.InvitationOrgRoles.AnyAsync(i => i.RoleId == roleId, cancellationToken)
                    || await db.Invitations.AnyAsync(i => i.TeamRoleId == roleId, cancellationToken);

        if (inUse)
            throw new OrganizationConflictException("Role is in use and cannot be deleted.");

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionSummaryDto>> ListPermissionsAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        return await LoadPermissionsAsync(role.Id, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionSummaryDto>?> ReplacePermissionsAsync(
        Guid organizationId,
        Guid roleId,
        AssignRolePermissionsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken);
        if (role is null)
            return null;

        EnsureCanEditPermissions(role);

        var permissions = await ResolvePermissionsAsync(organizationId, request.PermissionIds, cancellationToken);
        var existing = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(cancellationToken);
        db.RolePermissions.RemoveRange(existing);
        db.RolePermissions.AddRange(permissions.Select(p => new RolePermission { RoleId = roleId, PermissionId = p.Id }));
        await db.SaveChangesAsync(cancellationToken);

        return await LoadPermissionsAsync(roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionSummaryDto>?> AddPermissionsAsync(
        Guid organizationId,
        Guid roleId,
        AssignRolePermissionsRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken);
        if (role is null)
            return null;

        EnsureCanEditPermissions(role);

        var permissions = await ResolvePermissionsAsync(organizationId, request.PermissionIds, cancellationToken);
        var existingIds = await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);
        var existingSet = existingIds.ToHashSet();

        foreach (var permission in permissions.Where(p => !existingSet.Contains(p.Id)))
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });

        await db.SaveChangesAsync(cancellationToken);
        return await LoadPermissionsAsync(roleId, cancellationToken);
    }

    public async Task RemovePermissionAsync(
        Guid organizationId,
        Guid roleId,
        Guid permissionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgRolesManage, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        EnsureCanEditPermissions(role);

        var link = await db.RolePermissions
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Permission is not assigned to this role.");

        db.RolePermissions.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MemberSummaryDto>> ListMembersAsync(
        Guid organizationId,
        Guid roleId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);
        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        if (role.Scope != RoleScope.Org)
            throw new OrganizationValidationException("Only organization-scoped roles have organization members.");

        return await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.RoleId == roleId)
            .Join(
                db.OrganizationMembers.AsNoTracking(),
                assignment => new { assignment.OrganizationId, assignment.UserId },
                member => new { member.OrganizationId, member.UserId },
                (assignment, member) => new MemberSummaryDto
                {
                    UserId = member.UserId,
                    JoinedAt = member.JoinedAt
                })
            .OrderBy(m => m.JoinedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<MemberSummaryDto> AssignMemberAsync(
        Guid organizationId,
        Guid roleId,
        AssignRoleMemberRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        if (role.Scope != RoleScope.Org)
            throw new OrganizationValidationException("Only organization-scoped roles can be assigned to members.");

        var member = await db.OrganizationMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == request.UserId, cancellationToken)
            ?? throw new OrganizationValidationException("User is not a member of this organization.");

        if (role.Name == SystemRoleNames.Owner
            && !await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
            throw new OrganizationAccessException("Only owners can assign the Owner role.");

        if (await db.OrganizationMemberRoles.AnyAsync(
                m => m.OrganizationId == organizationId && m.UserId == request.UserId && m.RoleId == roleId,
                cancellationToken))
            throw new OrganizationConflictException("Role is already assigned to this member.");

        db.OrganizationMemberRoles.Add(new OrganizationMemberRole
        {
            OrganizationId = organizationId,
            UserId = request.UserId,
            RoleId = roleId,
            AssignedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return new MemberSummaryDto { UserId = member.UserId, JoinedAt = member.JoinedAt };
    }

    public async Task RevokeMemberAsync(
        Guid organizationId,
        Guid roleId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var role = await RequireRoleAsync(organizationId, roleId, cancellationToken)
            ?? throw new OrganizationNotFoundException("Role was not found.");

        if (role.Scope != RoleScope.Org)
            throw new OrganizationValidationException("Only organization-scoped roles can be revoked from members.");

        var assignment = await db.OrganizationMemberRoles
            .FirstOrDefaultAsync(
                m => m.OrganizationId == organizationId && m.UserId == userId && m.RoleId == roleId,
                cancellationToken)
            ?? throw new OrganizationNotFoundException("Role is not assigned to this member.");

        if (role.Name == SystemRoleNames.Owner)
        {
            if (!await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
                throw new OrganizationAccessException("Only owners can revoke the Owner role.");

            var ownerCount = await CountOwnersAsync(organizationId, cancellationToken);
            if (ownerCount <= 1)
                throw new OrganizationConflictException("Cannot remove or demote the last organization owner.");
        }

        db.OrganizationMemberRoles.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }

    async Task<Role?> RequireRoleAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.OrganizationId == organizationId, cancellationToken);

    static void EnsureCanEditPermissions(Role role)
    {
        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org)
            throw new OrganizationValidationException("Cannot modify permissions on the Owner role.");
    }

    async Task<List<Permission>> ResolvePermissionsAsync(
        Guid organizationId,
        IReadOnlyList<Guid> permissionIds,
        CancellationToken cancellationToken)
    {
        var distinct = permissionIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var permissions = await db.Permissions
            .Where(p => p.OrganizationId == organizationId && distinct.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (permissions.Count != distinct.Count)
            throw new OrganizationValidationException("One or more permission ids are invalid.");

        return permissions;
    }

    async Task<IReadOnlyList<RoleListItemResponse>> MapListAsync(IReadOnlyList<Role> roles, CancellationToken cancellationToken)
    {
        var roleIds = roles.Select(r => r.Id).ToList();

        var permissionCounts = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .GroupBy(rp => rp.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        var memberCounts = await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => roleIds.Contains(m.RoleId))
            .GroupBy(m => m.RoleId)
            .Select(g => new { RoleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, cancellationToken);

        return roles.Select(r => new RoleListItemResponse
        {
            Id = r.Id,
            OrganizationId = r.OrganizationId,
            Name = r.Name,
            Description = r.Description,
            Scope = r.Scope == RoleScope.Org ? "ORG" : "TEAM",
            IsSystem = r.IsSystem,
            CreatedAt = r.CreatedAt,
            MemberCount = memberCounts.GetValueOrDefault(r.Id),
            PermissionCount = permissionCounts.GetValueOrDefault(r.Id)
        }).ToList();
    }

    async Task<RoleDetailResponse> MapDetailAsync(Role role, CancellationToken cancellationToken)
    {
        var permissions = await LoadPermissionsAsync(role.Id, cancellationToken);
        var members = role.Scope == RoleScope.Org
            ? await db.OrganizationMemberRoles.AsNoTracking()
                .Where(m => m.RoleId == role.Id)
                .Join(
                    db.OrganizationMembers.AsNoTracking(),
                    assignment => new { assignment.OrganizationId, assignment.UserId },
                    member => new { member.OrganizationId, member.UserId },
                    (_, member) => new MemberSummaryDto
                    {
                        UserId = member.UserId,
                        JoinedAt = member.JoinedAt
                    })
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(cancellationToken)
            : [];

        return new RoleDetailResponse
        {
            Id = role.Id,
            OrganizationId = role.OrganizationId,
            Name = role.Name,
            Description = role.Description,
            Scope = role.Scope == RoleScope.Org ? "ORG" : "TEAM",
            IsSystem = role.IsSystem,
            CreatedAt = role.CreatedAt,
            MemberCount = members.Count,
            Members = members,
            Permissions = permissions
        };
    }

    async Task<IReadOnlyList<PermissionSummaryDto>> LoadPermissionsAsync(Guid roleId, CancellationToken cancellationToken) =>
        await db.RolePermissions.AsNoTracking()
            .Where(rp => rp.RoleId == roleId)
            .Join(
                db.Permissions.AsNoTracking(),
                rp => rp.PermissionId,
                p => p.Id,
                (_, p) => new PermissionSummaryDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Code = p.Code,
                    IsSystem = p.IsSystem
                })
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

    Task<int> CountOwnersAsync(Guid organizationId, CancellationToken cancellationToken) =>
        db.OrganizationMemberRoles
            .Join(db.Roles, m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .CountAsync(
                x => x.m.OrganizationId == organizationId
                     && x.r.Name == SystemRoleNames.Owner
                     && x.r.Scope == RoleScope.Org,
                cancellationToken);

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
