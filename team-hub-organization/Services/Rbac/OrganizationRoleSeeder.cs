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
        var permissions = await SeedOrganizationPermissionsAsync(db, organizationId, now, cancellationToken);

        var owner = CreateRole(organizationId, SystemRoleNames.Owner, RoleScope.Org, now, isSystem: true);
        var admin = CreateRole(organizationId, SystemRoleNames.Admin, RoleScope.Org, now, isSystem: true);
        var member = CreateRole(organizationId, SystemRoleNames.Member, RoleScope.Org, now, isSystem: true);
        var teamLead = CreateRole(organizationId, SystemRoleNames.TeamLead, RoleScope.Team, now, isSystem: true);
        var teamMember = CreateRole(organizationId, SystemRoleNames.Member, RoleScope.Team, now, isSystem: true);

        db.Roles.AddRange(owner, admin, member, teamLead, teamMember);

        AttachPermissions(db, owner, OrganizationPermissionCodes.All, permissions);
        AttachPermissions(db, admin, OrganizationPermissionCodes.AdminDefaults, permissions);
        AttachPermissions(db, teamLead, [OrganizationPermissionCodes.TeamManage, OrganizationPermissionCodes.TeamMembersManage], permissions);

        return new SeededRoles(owner, admin, member, teamLead, teamMember);
    }

    public static async Task<Dictionary<string, Permission>> SeedOrganizationPermissionsAsync(
        OrganizationDbContext db,
        Guid organizationId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var existing = await db.Permissions
            .Where(p => p.OrganizationId == organizationId)
            .ToDictionaryAsync(p => p.Code, cancellationToken);

        var missing = OrganizationPermissionCodes.Catalog
            .Where(p => !existing.ContainsKey(p.Code))
            .Select(p => new Permission
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Name = p.Name,
                Code = p.Code,
                Description = p.Description,
                IsSystem = true,
                CreatedAt = now
            })
            .ToList();

        if (missing.Count > 0)
        {
            db.Permissions.AddRange(missing);
            foreach (var permission in missing)
                existing[permission.Code] = permission;
        }

        return existing;
    }

    static Role CreateRole(Guid organizationId, string name, RoleScope scope, DateTimeOffset now, bool isSystem) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        Name = name,
        Scope = scope,
        IsSystem = isSystem,
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
}
