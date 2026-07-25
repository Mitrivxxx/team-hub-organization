using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
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

        var member = await db.OrganizationMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (member is null)
            return null;

        var roles = await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Join(
                db.Roles.AsNoTracking(),
                m => m.RoleId,
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

        var permissions = await authz.GetPermissionCodesAsync(organizationId, userId, cancellationToken);
        var teams = await memberService.ListTeamsAsync(organizationId, userId, userId, cancellationToken);

        return new MeResponse
        {
            UserId = userId,
            Roles = roles,
            JoinedAt = member.JoinedAt,
            Permissions = permissions,
            Teams = teams
        };
    }
}
