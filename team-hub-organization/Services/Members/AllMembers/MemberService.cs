using Microsoft.EntityFrameworkCore;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Members.AllMembers;

public sealed class MemberService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IActivityRecorder activity) : IMemberService
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
        {
            var userIdsWithRole = db.OrganizationMemberRoles.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.RoleId == roleId)
                .Select(m => m.UserId);
            query = query.Where(m => userIdsWithRole.Contains(m.UserId));
        }

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

        var members = await query.OrderBy(m => m.JoinedAt).ToListAsync(cancellationToken);
        return await MapMembersAsync(organizationId, members, cancellationToken);
    }

    public async Task<MemberResponse?> GetAsync(
        Guid organizationId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var member = await db.OrganizationMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (member is null)
            return null;

        var mapped = await MapMembersAsync(organizationId, [member], cancellationToken);
        return mapped[0];
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

        var roles = await ResolveOrgRolesAsync(organizationId, request.RoleIds, cancellationToken);
        foreach (var role in roles)
            await EnsureCanAssignRoleAsync(organizationId, actorUserId, role, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = request.UserId,
            JoinedAt = now
        });

        foreach (var role in roles)
        {
            db.OrganizationMemberRoles.Add(new OrganizationMemberRole
            {
                OrganizationId = organizationId,
                UserId = request.UserId,
                RoleId = role.Id,
                AssignedAt = now
            });
        }

        activity.Record(
            organizationId,
            ActivityTypes.MemberJoined,
            actorUserId,
            targetUserId: request.UserId,
            entityType: ActivityEntityTypes.Member,
            entityId: request.UserId,
            details: new { roles = roles.Select(r => r.Name).ToArray() },
            occurredAt: now);

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

        var newRoles = await ResolveOrgRolesAsync(organizationId, request.RoleIds, cancellationToken);
        foreach (var role in newRoles)
            await EnsureCanAssignRoleAsync(organizationId, actorUserId, role, cancellationToken);

        var existing = await db.OrganizationMemberRoles
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .ToListAsync(cancellationToken);

        var fromRoleNames = await db.Roles.AsNoTracking()
            .Where(r => existing.Select(e => e.RoleId).Contains(r.Id))
            .Select(r => r.Name)
            .OrderBy(n => n)
            .ToListAsync(cancellationToken);

        var currentOwnerAssignment = await db.OrganizationMemberRoles
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .Join(db.Roles, m => m.RoleId, r => r.Id, (m, r) => r)
            .AnyAsync(r => r.Name == SystemRoleNames.Owner && r.Scope == RoleScope.Org, cancellationToken);

        var newHasOwner = newRoles.Any(r => r.Name == SystemRoleNames.Owner && r.Scope == RoleScope.Org);
        if (currentOwnerAssignment && !newHasOwner)
            await EnsureNotLastOwnerAsync(organizationId, cancellationToken);

        db.OrganizationMemberRoles.RemoveRange(existing);
        var now = DateTimeOffset.UtcNow;
        foreach (var role in newRoles)
        {
            db.OrganizationMemberRoles.Add(new OrganizationMemberRole
            {
                OrganizationId = organizationId,
                UserId = userId,
                RoleId = role.Id,
                AssignedAt = now
            });
        }

        var toRoleNames = newRoles.Select(r => r.Name).OrderBy(n => n).ToArray();
        activity.Record(
            organizationId,
            ActivityTypes.MemberRolesChanged,
            actorUserId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Member,
            entityId: userId,
            details: new { fromRoles = fromRoleNames, toRoles = toRoleNames },
            occurredAt: now);

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
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, cancellationToken);

        if (member is null)
            return false;

        if (await authz.IsOwnerAsync(organizationId, userId, cancellationToken))
            await EnsureNotLastOwnerAsync(organizationId, cancellationToken);

        var roleAssignments = await db.OrganizationMemberRoles
            .Where(m => m.OrganizationId == organizationId && m.UserId == userId)
            .ToListAsync(cancellationToken);

        var teamMemberships = await db.TeamMembers
            .Where(tm => tm.UserId == userId)
            .Join(
                db.Teams.Where(t => t.OrganizationId == organizationId),
                tm => tm.TeamId,
                t => t.Id,
                (tm, _) => tm)
            .ToListAsync(cancellationToken);

        db.OrganizationMemberRoles.RemoveRange(roleAssignments);
        db.TeamMembers.RemoveRange(teamMemberships);
        db.OrganizationMembers.Remove(member);

        activity.Record(
            organizationId,
            ActivityTypes.MemberLeft,
            actorUserId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Member,
            entityId: userId);

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

    async Task<IReadOnlyList<MemberResponse>> MapMembersAsync(
        Guid organizationId,
        IReadOnlyList<OrganizationMember> members,
        CancellationToken cancellationToken)
    {
        var userIds = members.Select(m => m.UserId).ToList();
        var roleRows = await db.OrganizationMemberRoles.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && userIds.Contains(m.UserId))
            .Join(
                db.Roles.AsNoTracking(),
                m => m.RoleId,
                r => r.Id,
                (m, r) => new { m.UserId, Role = r })
            .ToListAsync(cancellationToken);

        var rolesByUser = roleRows
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<RoleSummaryDto>)g
                    .Select(x => new RoleSummaryDto
                    {
                        Id = x.Role.Id,
                        Name = x.Role.Name,
                        Scope = x.Role.Scope == RoleScope.Org ? "ORG" : "TEAM",
                        IsSystem = x.Role.IsSystem
                    })
                    .OrderBy(r => r.Name)
                    .ToList());

        var teamIdsByUser = await LoadTeamIdsByUserAsync(organizationId, cancellationToken);

        return members.Select(m => new MemberResponse
        {
            UserId = m.UserId,
            Roles = rolesByUser.GetValueOrDefault(m.UserId, []),
            JoinedAt = m.JoinedAt,
            TeamIds = teamIdsByUser.GetValueOrDefault(m.UserId, [])
        }).ToList();
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

    async Task<List<Role>> ResolveOrgRolesAsync(Guid organizationId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        var distinct = roleIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinct.Count == 0)
            throw new OrganizationValidationException("At least one roleId is required.");

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.Scope == RoleScope.Org && distinct.Contains(r.Id))
            .ToListAsync(cancellationToken);

        if (roles.Count != distinct.Count)
            throw new OrganizationValidationException("One or more roles were not found or are not organization-scoped.");

        return roles;
    }

    async Task EnsureCanAssignRoleAsync(Guid organizationId, Guid actorUserId, Role role, CancellationToken cancellationToken)
    {
        if (role.Name == SystemRoleNames.Owner && role.Scope == RoleScope.Org
            && !await authz.IsOwnerAsync(organizationId, actorUserId, cancellationToken))
            throw new OrganizationAccessException("Only owners can assign the Owner role.");
    }

    async Task EnsureNotLastOwnerAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var ownerCount = await db.OrganizationMemberRoles
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
