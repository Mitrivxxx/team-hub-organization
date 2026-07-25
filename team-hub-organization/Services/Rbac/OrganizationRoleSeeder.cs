using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Rbac;

public static class OrganizationRoleSeeder
{
    public sealed record SeededRoles(
        Role Owner,
        Role Admin,
        Role Member,
        Role TeamLead,
        Role TeamMember);

    public static async Task<SeededRoles> SeedSystemRolesAsync(
        OrganizationDbContext db,
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await EnsurePermissionsExistAsync(db, cancellationToken);

        var permissions = await db.Permissions.ToDictionaryAsync(p => p.Code, cancellationToken);

        var owner = CreateRole(organizationId, SystemRoleNames.Owner, RoleScope.Org, now);
        var admin = CreateRole(organizationId, SystemRoleNames.Admin, RoleScope.Org, now);
        var member = CreateRole(organizationId, SystemRoleNames.Member, RoleScope.Org, now);
        var teamLead = CreateRole(organizationId, SystemRoleNames.TeamLead, RoleScope.Team, now);
        var teamMember = CreateRole(organizationId, SystemRoleNames.Member, RoleScope.Team, now);

        db.Roles.AddRange(owner, admin, member, teamLead, teamMember);

        AttachPermissions(db, owner, OrganizationPermissionCodes.All, permissions);
        AttachPermissions(db, admin, OrganizationPermissionCodes.AdminDefaults, permissions);
        AttachPermissions(db, teamLead, [OrganizationPermissionCodes.TeamManage, OrganizationPermissionCodes.TeamMembersManage], permissions);

        return new SeededRoles(owner, admin, member, teamLead, teamMember);
    }

    static Role CreateRole(Guid organizationId, string name, RoleScope scope, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Name = name,
        Scope = scope,
        CreatedAt = now
    };

    static void AttachPermissions(
        OrganizationDbContext db,
        Role role,
        IEnumerable<string> codes,
        IReadOnlyDictionary<string, Permission> permissions)
    {
        foreach (var code in codes)
        {
            if (!permissions.TryGetValue(code, out var permission))
                continue;

            db.RolePermissions.Add(new RolePermission
            {
                RoleId = role.Id,
                PermissionId = permission.Id
            });
        }
    }

    static async Task EnsurePermissionsExistAsync(OrganizationDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.Permissions
            .Select(p => p.Code)
            .ToListAsync(cancellationToken);

        var existingSet = existing.ToHashSet(StringComparer.Ordinal);
        var missing = OrganizationPermissionCodes.Catalog
            .Where(p => !existingSet.Contains(p.Code))
            .Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                Code = p.Code,
                Description = p.Description
            })
            .ToList();

        if (missing.Count == 0)
            return;

        db.Permissions.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
