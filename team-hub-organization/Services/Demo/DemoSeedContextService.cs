using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TeamHub.DemoSeed;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Auth;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Services.Demo;

public interface IDemoSeedContextService
{
    Task<DemoSeedContextResponse?> GetAsync(int organizationIndex, CancellationToken cancellationToken = default);
}

public sealed class DemoSeedContextService(
    OrganizationDbContext db,
    IAuthUserResolveClient authUsers,
    IOptions<SeedOptions> seedOptions,
    ILogger<DemoSeedContextService> logger) : IDemoSeedContextService
{
    const int SampleMemberLimit = 5;

    public async Task<DemoSeedContextResponse?> GetAsync(
        int organizationIndex,
        CancellationToken cancellationToken = default)
    {
        if (organizationIndex < 1)
            return null;

        var seed = seedOptions.Value;
        var slug = $"{seed.SlugPrefix}-{organizationIndex}";
        var org = await db.Organizations.AsNoTracking()
            .Where(o => o.Slug == slug && o.DeletedAt == null)
            .Select(o => new { o.Id, o.Slug, o.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (org is null)
            return null;

        var roles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == org.Id)
            .Select(r => new { r.Id, r.Name, r.Scope })
            .ToListAsync(cancellationToken);

        Guid RoleId(string name, RoleScope scope) =>
            roles.First(r => r.Name == name && r.Scope == scope).Id;

        var ownerRoleId = RoleId(SystemRoleNames.Owner, RoleScope.Org);
        var ownerUserId = await db.OrganizationMemberRoles.AsNoTracking()
            .Where(mr => mr.OrganizationId == org.Id && mr.RoleId == ownerRoleId)
            .Select(mr => mr.UserId)
            .FirstAsync(cancellationToken);

        var sampleMemberUserIds = await db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == org.Id && m.UserId != ownerUserId)
            .OrderBy(m => m.JoinedAt)
            .Select(m => m.UserId)
            .Take(SampleMemberLimit)
            .ToListAsync(cancellationToken);

        var teams = await db.Teams.AsNoTracking()
            .Where(t => t.OrganizationId == org.Id && t.DeletedAt == null)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new DemoSeedTeamContext { Id = t.Id, Name = t.Name })
            .ToListAsync(cancellationToken);

        var permissions = await db.Permissions.AsNoTracking()
            .Where(p => p.OrganizationId == org.Id)
            .OrderBy(p => p.Code)
            .Select(p => new DemoSeedPermissionContext { Id = p.Id, Code = p.Code })
            .ToListAsync(cancellationToken);

        var pendingInvitations = await db.Invitations.AsNoTracking()
            .Where(i => i.OrganizationId == org.Id
                        && i.Status == InvitationStatus.Pending
                        && i.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new DemoSeedInvitationContext { Id = i.Id, Email = i.Email })
            .ToListAsync(cancellationToken);

        Guid? addMemberUserId = null;
        string? addMemberUsername = null;
        var addMemberUsernameCandidate = ResolveAddMemberUsername(seed);
        if (addMemberUsernameCandidate is not null)
        {
            try
            {
                var resolved = await authUsers.ResolveUsersAsync(
                    emails: [],
                    usernames: [addMemberUsernameCandidate],
                    cancellationToken);
                var candidate = resolved.Users.FirstOrDefault(u =>
                    string.Equals(u.Username, addMemberUsernameCandidate, StringComparison.OrdinalIgnoreCase));
                if (candidate is not null)
                {
                    var alreadyMember = await db.OrganizationMembers.AsNoTracking()
                        .AnyAsync(
                            m => m.OrganizationId == org.Id && m.UserId == candidate.Id,
                            cancellationToken);
                    if (!alreadyMember)
                    {
                        addMemberUserId = candidate.Id;
                        addMemberUsername = candidate.Username;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Demo context: could not resolve '{Username}' via auth gRPC",
                    addMemberUsernameCandidate);
            }
        }

        return new DemoSeedContextResponse
        {
            OrganizationId = org.Id,
            Slug = org.Slug,
            Name = org.Name,
            OwnerUserId = ownerUserId,
            Roles = new DemoSeedRolesContext
            {
                OrgOwner = ownerRoleId,
                OrgAdmin = RoleId(SystemRoleNames.Admin, RoleScope.Org),
                OrgMember = RoleId(SystemRoleNames.Member, RoleScope.Org),
                TeamLead = RoleId(SystemRoleNames.TeamLead, RoleScope.Team),
                TeamMember = RoleId(SystemRoleNames.Member, RoleScope.Team)
            },
            Teams = teams,
            SampleMemberUserIds = sampleMemberUserIds,
            AddMemberUserId = addMemberUserId,
            AddMemberUsername = addMemberUsername,
            Permissions = permissions,
            PendingInvitations = pendingInvitations
        };
    }

    static string? ResolveAddMemberUsername(SeedOptions seed)
    {
        // First bulk user after MembersPerOrganization is intentionally outside the seeded org.
        var index = seed.MembersPerOrganization;
        var usernames = DemoUserIdentityFactory.CreateUsernames(
            index + 1,
            reservedUsername: seed.OwnerUsername);
        return usernames.Count > index ? usernames[index] : null;
    }
}
