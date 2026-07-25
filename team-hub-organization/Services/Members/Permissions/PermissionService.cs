using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;

namespace team_hub_organization.Services.Members.Permissions;

public sealed class PermissionService(OrganizationDbContext db) : IPermissionService
{
    public async Task<IReadOnlyList<PermissionResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await db.Permissions.AsNoTracking()
            .OrderBy(p => p.Code)
            .Select(p => new PermissionResponse
            {
                Id = p.Id,
                Code = p.Code,
                Description = p.Description
            })
            .ToListAsync(cancellationToken);
    }
}
