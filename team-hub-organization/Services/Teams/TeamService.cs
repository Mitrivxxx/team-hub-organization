using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamHub.BlobStorage;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Members.Activity;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Teams;

public sealed class TeamService(
    OrganizationDbContext db,
    IOrganizationAuthorizationService authz,
    IActivityRecorder activity,
    IOptions<OrganizationQuotasOptions> quotas,
    IServiceProvider serviceProvider) : ITeamService
{
    IBlobStorageService? BlobStorage => serviceProvider.GetService<IBlobStorageService>();

    public async Task<IReadOnlyList<TeamResponse>> ListAsync(
        Guid organizationId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);

        var teams = await db.Teams.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.DeletedAt == null)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var counts = await db.TeamMembers.AsNoTracking()
            .Where(tm => teams.Select(t => t.Id).Contains(tm.TeamId))
            .GroupBy(tm => tm.TeamId)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = counts.ToDictionary(x => x.TeamId, x => x.Count);
        return teams.Select(t => ToResponse(t, countMap.GetValueOrDefault(t.Id))).ToList();
    }

    public async Task<TeamResponse?> GetAsync(
        Guid organizationId,
        Guid teamId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);
        var team = await FindTeamAsync(organizationId, teamId, cancellationToken);
        if (team is null)
            return null;

        var count = await db.TeamMembers.AsNoTracking().CountAsync(tm => tm.TeamId == teamId, cancellationToken);
        return ToResponse(team, count);
    }

    public async Task<TeamResponse> CreateAsync(
        Guid organizationId,
        CreateTeamRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        var teamCount = await db.Teams.CountAsync(
            t => t.OrganizationId == organizationId && t.DeletedAt == null,
            cancellationToken);
        if (teamCount >= quotas.Value.MaxTeamsPerOrg)
            throw new OrganizationQuotaExceededException(
                $"Team quota exceeded (max {quotas.Value.MaxTeamsPerOrg} teams per organization).");

        var now = DateTimeOffset.UtcNow;
        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Teams.Add(team);
        activity.Record(
            organizationId,
            ActivityTypes.TeamCreated,
            actorUserId,
            entityType: ActivityEntityTypes.Team,
            entityId: team.Id,
            details: new { teamName = team.Name },
            occurredAt: now);
        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(team, 0);
    }

    public async Task<TeamResponse?> UpdateAsync(
        Guid organizationId,
        Guid teamId,
        UpdateTeamRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

        if (team is null)
            return null;

        if (!string.IsNullOrWhiteSpace(request.Name))
            team.Name = request.Name.Trim();

        if (request.Description is not null)
            team.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        team.UpdatedAt = DateTimeOffset.UtcNow;
        activity.Record(
            organizationId,
            ActivityTypes.TeamUpdated,
            actorUserId,
            entityType: ActivityEntityTypes.Team,
            entityId: team.Id,
            details: new { teamName = team.Name });
        await db.SaveChangesAsync(cancellationToken);

        var count = await db.TeamMembers.CountAsync(tm => tm.TeamId == teamId, cancellationToken);
        return ToResponse(team, count);
    }

    public async Task<bool> SoftDeleteAsync(
        Guid organizationId,
        Guid teamId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        var team = await db.Teams
            .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

        if (team is null)
            return false;

        team.DeletedAt = DateTimeOffset.UtcNow;
        team.UpdatedAt = team.DeletedAt.Value;

        var teamMembers = await db.TeamMembers
            .Where(tm => tm.TeamId == teamId)
            .ToListAsync(cancellationToken);
        db.TeamMembers.RemoveRange(teamMembers);

        activity.Record(
            organizationId,
            ActivityTypes.TeamDeleted,
            actorUserId,
            entityType: ActivityEntityTypes.Team,
            entityId: team.Id,
            details: new { teamName = team.Name });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<TeamMemberResponse>> ListMembersAsync(
        Guid organizationId,
        Guid teamId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsureMemberAsync(organizationId, actorUserId, cancellationToken);
        if (await FindTeamAsync(organizationId, teamId, cancellationToken) is null)
            throw new OrganizationNotFoundException("Team was not found.");

        return await QueryTeamMembers(teamId).ToListAsync(cancellationToken);
    }

    public async Task<TeamMemberResponse> AddMemberAsync(
        Guid organizationId,
        Guid teamId,
        AddTeamMemberRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        if (await FindTeamAsync(organizationId, teamId, cancellationToken) is null)
            throw new OrganizationNotFoundException("Team was not found.");

        if (!await authz.IsMemberAsync(organizationId, request.UserId, cancellationToken))
            throw new OrganizationValidationException("User must be an organization member before joining a team.");

        if (await db.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == request.UserId, cancellationToken))
            throw new OrganizationConflictException("User is already a member of this team.");

        var role = await GetTeamRoleAsync(organizationId, request.RoleId, cancellationToken);
        var team = await FindTeamAsync(organizationId, teamId, cancellationToken);

        db.TeamMembers.Add(new TeamMember
        {
            TeamId = teamId,
            UserId = request.UserId,
            RoleId = role.Id,
            JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim(),
            JoinedAt = DateTimeOffset.UtcNow
        });
        activity.Record(
            organizationId,
            ActivityTypes.TeamMemberAdded,
            actorUserId,
            targetUserId: request.UserId,
            entityType: ActivityEntityTypes.Team,
            entityId: teamId,
            details: new { teamName = team!.Name, roleName = role.Name });
        await db.SaveChangesAsync(cancellationToken);

        return await QueryTeamMembers(teamId).FirstAsync(m => m.UserId == request.UserId, cancellationToken);
    }

    public async Task<TeamMemberResponse?> UpdateMemberAsync(
        Guid organizationId,
        Guid teamId,
        Guid userId,
        UpdateTeamMemberRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        if (await FindTeamAsync(organizationId, teamId, cancellationToken) is null)
            throw new OrganizationNotFoundException("Team was not found.");

        var member = await db.TeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);

        if (member is null)
            return null;

        if (request.RoleId is Guid roleId)
            member.RoleId = (await GetTeamRoleAsync(organizationId, roleId, cancellationToken)).Id;

        if (request.JobTitle is not null)
            member.JobTitle = string.IsNullOrWhiteSpace(request.JobTitle) ? null : request.JobTitle.Trim();

        var team = await FindTeamAsync(organizationId, teamId, cancellationToken);
        var roleName = await db.Roles.AsNoTracking()
            .Where(r => r.Id == member.RoleId)
            .Select(r => r.Name)
            .FirstAsync(cancellationToken);

        activity.Record(
            organizationId,
            ActivityTypes.TeamMemberUpdated,
            actorUserId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Team,
            entityId: teamId,
            details: new { teamName = team!.Name, roleName, jobTitle = member.JobTitle });
        await db.SaveChangesAsync(cancellationToken);
        return await QueryTeamMembers(teamId).FirstAsync(m => m.UserId == userId, cancellationToken);
    }

    public async Task<bool> RemoveMemberAsync(
        Guid organizationId,
        Guid teamId,
        Guid userId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await authz.EnsurePermissionAsync(organizationId, actorUserId, OrganizationPermissionCodes.OrgTeamsManage, cancellationToken);

        if (await FindTeamAsync(organizationId, teamId, cancellationToken) is null)
            throw new OrganizationNotFoundException("Team was not found.");

        var member = await db.TeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == teamId && tm.UserId == userId, cancellationToken);

        if (member is null)
            return false;

        var team = await FindTeamAsync(organizationId, teamId, cancellationToken);
        db.TeamMembers.Remove(member);
        activity.Record(
            organizationId,
            ActivityTypes.TeamMemberRemoved,
            actorUserId,
            targetUserId: userId,
            entityType: ActivityEntityTypes.Team,
            entityId: teamId,
            details: new { teamName = team!.Name });
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    Task<Team?> FindTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
        db.Teams.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == teamId && t.OrganizationId == organizationId && t.DeletedAt == null, cancellationToken);

    async Task<Role> GetTeamRoleAsync(Guid organizationId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.Id == roleId && r.OrganizationId == organizationId && r.Scope == RoleScope.Team,
                cancellationToken);

        return role ?? throw new OrganizationValidationException("Role was not found or is not a team-scoped role.");
    }

    IQueryable<TeamMemberResponse> QueryTeamMembers(Guid teamId) =>
        db.TeamMembers.AsNoTracking()
            .Where(tm => tm.TeamId == teamId)
            .Join(db.Roles.AsNoTracking(), tm => tm.RoleId, r => r.Id, (tm, r) => new TeamMemberResponse
            {
                UserId = tm.UserId,
                RoleId = r.Id,
                RoleName = r.Name,
                JobTitle = tm.JobTitle,
                JoinedAt = tm.JoinedAt
            })
            .OrderBy(m => m.JoinedAt);

    TeamResponse ToResponse(Team team, int memberCount) => new()
    {
        Id = team.Id,
        OrganizationId = team.OrganizationId,
        Name = team.Name,
        Description = team.Description,
        AvatarUrl = ResolveAvatarUrl(team.AvatarUrl),
        MemberCount = memberCount,
        CreatedAt = team.CreatedAt,
        UpdatedAt = team.UpdatedAt
    };

    string? ResolveAvatarUrl(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath) || !BlobStoragePaths.IsTeamAvatarPath(blobPath))
            return null;
        return BlobStorage?.GetReadSasUri(blobPath)?.ToString();
    }
}
