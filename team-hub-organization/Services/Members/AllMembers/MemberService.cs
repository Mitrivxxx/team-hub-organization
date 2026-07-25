using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.AllMembers;

public sealed class MemberService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz) : IMemberService
{
    public async Task<IReadOnlyList<MemberResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        Guid? roleId = null,
        Guid? teamId = null,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var query = db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId);

        if (roleId is not null)
            query = query.Where(m => m.RoleId == roleId);

        if (teamId is not null)
        {
            var teamUserIds = db.TeamMembers.AsNoTracking()
                .Where(tm => tm.TeamId == teamId)
                .Join(
                    db.Teams.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeletedAt == null),
                    tm => tm.TeamId,
                    t => t.Id,
                    (tm, _) => tm.UserId);

            query = query.Where(m => teamUserIds.Contains(m.UserId));
        }

        var members = await query
            .Join(db.Roles.AsNoTracking(), m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .OrderBy(x => x.m.JoinedAt)
            .ToListAsync(cancellationToken);

        var teamIdsByUser = await LoadTeamIdsByUserAsync(organizationId, cancellationToken);

        return members.Select(x => new MemberResponse
        {
            UserId = x.m.UserId,
            RoleId = x.r.Id,
            RoleName = x.r.Name,
            JoinedAt = x.m.JoinedAt,
            TeamIds = teamIdsByUser.GetValueOrDefault(x.m.UserId, [])
        }).ToList();
    }

    public async Task<MemberResponse?> GetAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var row = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Join(db.Roles.AsNoTracking(), m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var teamIdsByUser = await LoadTeamIdsByUserAsync(organizationId, cancellationToken);

        return new MemberResponse
        {
            UserId = row.m.UserId,
            RoleId = row.r.Id,
            RoleName = row.r.Name,
            JoinedAt = row.m.JoinedAt,
            TeamIds = teamIdsByUser.GetValueOrDefault(row.m.UserId, [])
        };
    }

    public async Task<MemberResponse> AddAsync(
        Guid organizationId,
        AddMemberRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        if (await db.OrganizationMembers.AnyAsync(
                m => m.OrganizationId == organizationId && m.UserId == request.UserId,
                cancellationToken))
            throw new OrganizationConflictException("User is already a member of this organization.");

        var role = await GetOrgRoleAsync(organizationId, request.RoleId, cancellationToken);
        await EnsureCanAssignRoleAsync(organizationId, actorUserId, role, cancellationToken);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = request.UserId,
            RoleId = role.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return (await GetAsync(organizationId, request.UserId, actorUserId, cancellationToken))!;
    }

    public async Task<MemberResponse?> UpdateAsync(
        Guid organizationId,
        Guid userId,
        UpdateMemberRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var member = await db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (member is null)
            return null;

        var currentRole = await db.Roles.AsNoTracking().FirstAsync(r => r.Id == member.RoleId, cancellationToken);
        var newRole = await GetOrgRoleAsync(organizationId, request.RoleId, cancellationToken);

        if (currentRole.Name == SystemRoleNames.Owner && currentRole.Scope == RoleScope.Org)
            await EnsureNotLastOwnerAsync(organizationId, cancellationToken);

        await EnsureCanAssignRoleAsync(organizationId, actorUserId, newRole, cancellationToken);

        member.RoleId = newRole.Id;
        await db.SaveChangesAsync(cancellationToken);

        return await GetAsync(organizationId, userId, actorUserId, cancellationToken);
    }

    public async Task<bool> RemoveAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgMembersManage, cancellationToken);

        var member = await db.OrganizationMembers
            .Include(m => m.Role)
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (member is null)
            return false;

        if (member.Role.Name == SystemRoleNames.Owner && member.Role.Scope == RoleScope.Org)
            await EnsureNotLastOwnerAsync(organizationId, cancellationToken);

        var teamMemberships = await db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Join(
                db.Teams.Where(t => t.OrganizationId == organizationId),
                tm => tm.TeamId,
                t => t.Id,
                (tm, _) => tm)
            .ToListAsync(cancellationToken);

        db.TeamMembers.RemoveRange(teamMemberships);
        db.OrganizationMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TeamMembershipResponse>> ListTeamsAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        if (!await authz.IsMemberAsync(organizationId, userId, cancellationToken))
            return [];

        return await db.TeamMembers.AsNoTracking()
            .Where(tm => tm.UserId == userId)
            .Join(
                db.Teams.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeletedAt == null),
                tm => tm.TeamId,
                t => t.Id,
                (tm, t) => new { tm, t })
            .Join(
                db.Roles.AsNoTracking(),
                x => x.tm.RoleId,
                r => r.Id,
                (x, r) => new TeamMembershipResponse
                {
                    TeamId = x.t.Id,
                    TeamName = x.t.Name,
                    RoleId = r.Id,
                    RoleName = r.Name,
                    JobTitle = x.tm.JobTitle,
                    JoinedAt = x.tm.JoinedAt
                })
            .OrderBy(t => t.TeamName)
            .ToListAsync(cancellationToken);
    }

    async Task<Dictionary<Guid, IReadOnlyList<Guid>>> LoadTeamIdsByUserAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var rows = await db.TeamMembers.AsNoTracking()
            .Join(
                db.Teams.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.DeletedAt == null),
                tm => tm.TeamId,
                t => t.Id,
                (tm, _) => tm)
            .Select(tm => new { tm.UserId, tm.TeamId })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(x => x.TeamId).ToList());
    }

    async Task<Role> GetOrgRoleAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == roleId && r.OrganizationId == organizationId && r.Scope == RoleScope.Org,
                cancellationToken);

        return role ?? throw new OrganizationValidationException("Role was not found or is not an organization-scoped role.");
    }

    async Task EnsureCanAssignRoleAsync(Guid organizationId, Guid actorUserId, Role role, CancellationToken cancellationToken)
    {
        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org
            && !await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
            throw new OrganizationAccessException("Only owners can assign the Owner role.");
    }

    async Task EnsureNotLastOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var ownerCount = await db.OrganizationMembers
            .Join(db.Roles, m => m.RoleId, r => r.Id, (m, r) => new { m, r })
            .CountAsync(
                x => x.m.OrganizationId == organizationId
                     && x.r.Name == SystemRoleNames.Owner
                     && x.r.Scope == RoleScope.Org,
                cancellationToken);

        if (ownerCount <= 1)
            throw new OrganizationConflictException("Cannot remove or demote the last organization owner.");
    }
}
