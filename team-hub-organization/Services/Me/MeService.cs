using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Me;

public interface IMeService
{
    Task<MeResponse?> GetAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default);
}

public sealed class MeService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IMemberService memberService) : IMeService
{
    public async Task<MeResponse?> GetAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, userId, cancellationToken);

        var row = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Join(db.Roles.AsNoTracking(), m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var permissions = await authz.GetPermissionCodesAsync(organizationId, userId, cancellationToken);
        var teams = await memberService.ListTeamsAsync(organizationId, userId, userId, cancellationToken);

        return new MeResponse
        {
            UserId = userId,
            RoleId = row.r.Id,
            RoleName = row.r.Name,
            JoinedAt = row.m.JoinedAt,
            Permissions = permissions,
            Teams = teams
        };
    }
}
