using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Statistics;

public interface IOrganizationStatsService
{
    Task<OrganizationStatsResponse> GetAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class OrganizationStatsService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz) : IOrganizationStatsService
{
    public async Task<OrganizationStatsResponse> GetAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var memberCount = await db.OrganizationMembers.AsNoTracking()
            .CountAsync(m => m.OrganizationId == organizationId, cancellationToken);

        var teamCount = await db.Teams.AsNoTracking()
            .CountAsync(t => t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

        return new OrganizationStatsResponse
        {
            MemberCount = memberCount,
            TeamCount = teamCount
        };
    }
}
