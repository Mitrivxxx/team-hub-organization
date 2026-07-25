using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Models;

namespace team_hub_organization.Services.Rbac;

public sealed class PermissionSeedService(OrganizationDbContext db) : IPermissionSeedService
{
    public async Task EnsureCatalogAsync(CancellationToken cancellationToken = default)
    {
        var existing = await db.Permissions
            .AsNoTracking()
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
