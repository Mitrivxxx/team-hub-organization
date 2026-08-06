using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Auth;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Members.Invitations;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Seeding.Internal;

public sealed class OrganizationDemoBuilder(
    OrganizationDbContext db,
    IOrganizationService organizations,
    IMemberService members,
    ITeamService teams,
    IInvitationService invitations,
    IAuthUserResolveClient authUsers,
    IOptions<SeedOptions> options,
    ILogger<OrganizationDemoBuilder> logger)
{
    const int ResolveBatchSize = 100;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var seed = options.Value;
        if (seed.OrganizationCount <= 0)
        {
            logger.LogInformation("Organization demo seed skipped: OrganizationCount is 0");
            return;
        }

        var owner = await ResolveOwnerAsync(seed.OwnerUsername, cancellationToken);
        logger.LogInformation(
            "Seeding {OrgCount} demo organization(s) for owner '{Username}' ({UserId})",
            seed.OrganizationCount,
            owner.Username,
            owner.Id);

        for (var i = 1; i <= seed.OrganizationCount; i++)
        {
            await SeedOrganizationAsync(seed, owner.Id, i, cancellationToken);
        }
    }

    async Task SeedOrganizationAsync(
        SeedOptions seed,
        Guid ownerUserId,
        int index,
        CancellationToken cancellationToken)
    {
        var slug = $"{seed.SlugPrefix}-{index}";
        var exists = await db.Organizations.AsNoTracking()
            .AnyAsync(o => o.Slug == slug && o.DeletedAt == null, cancellationToken);

        if (exists)
        {
            logger.LogInformation("Demo organization '{Slug}' already exists — skipped", slug);
            return;
        }

        var company = DemoOrganizationCatalog.GetCompany(index);
        var nip = BuildNip(seed.NipBase, index);
        var org = await organizations.CreateAsync(
            new CreateOrganizationRequest
            {
                Name = company.Name,
                Slug = slug,
                Description = company.Description,
                Nip = nip,
                Address = company.Address
            },
            ownerUserId,
            cancellationToken);

        logger.LogInformation("Created demo organization '{Slug}' ({OrgId})", org.Slug, org.Id);

        var orgRoles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == org.Id && r.Scope == RoleScope.Org)
            .ToDictionaryAsync(r => r.Name, r => r.Id, cancellationToken);

        var adminRoleId = orgRoles[SystemRoleNames.Admin];
        var memberRoleId = orgRoles[SystemRoleNames.Member];

        var memberUserIds = await AddDemoMembersAsync(
            org.Id,
            ownerUserId,
            adminRoleId,
            memberRoleId,
            seed.MembersPerOrganization,
            seed.AdminCountPerOrganization,
            cancellationToken);

        var createdTeams = await AddDemoTeamsAsync(
            org.Id,
            ownerUserId,
            seed.TeamsPerOrganization,
            cancellationToken);

        var teamRoles = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == org.Id && r.Scope == RoleScope.Team)
            .ToDictionaryAsync(r => r.Name, r => r.Id, cancellationToken);

        await PopulateTeamsAsync(
            org.Id,
            ownerUserId,
            createdTeams,
            memberUserIds,
            teamRoles[SystemRoleNames.TeamLead],
            teamRoles[SystemRoleNames.Member],
            cancellationToken);

        await AddPendingInvitationsAsync(
            org.Id,
            ownerUserId,
            memberRoleId,
            createdTeams.FirstOrDefault()?.Id,
            teamRoles[SystemRoleNames.Member],
            seed.PendingInvitationCount,
            cancellationToken);
    }

    async Task<IReadOnlyList<Guid>> AddDemoMembersAsync(
        Guid organizationId,
        Guid ownerUserId,
        Guid adminRoleId,
        Guid memberRoleId,
        int memberCount,
        int adminCount,
        CancellationToken cancellationToken)
    {
        if (memberCount <= 0)
            return [];

        var usernames = Enumerable.Range(1, memberCount)
            .Select(n => $"demo{n:D5}")
            .ToList();

        var resolved = await ResolveUsernamesAsync(usernames, cancellationToken);
        if (resolved.Count == 0)
        {
            logger.LogWarning(
                "No demo users resolved for organization {OrgId}; seed auth first (demo00001…)",
                organizationId);
            return [];
        }

        var candidates = resolved.Where(u => u.Id != ownerUserId).ToList();
        var addedUserIds = new List<Guid>(candidates.Count);
        var adminsAdded = 0;
        var membersAdded = 0;

        foreach (var user in candidates)
        {
            var roleId = adminsAdded < adminCount ? adminRoleId : memberRoleId;

            try
            {
                await members.AddAsync(
                    organizationId,
                    new AddMemberRequest
                    {
                        UserId = user.Id,
                        RoleIds = [roleId]
                    },
                    ownerUserId,
                    cancellationToken);
                addedUserIds.Add(user.Id);
                if (roleId == adminRoleId)
                    adminsAdded++;
                else
                    membersAdded++;
            }
            catch (OrganizationConflictException)
            {
                // already a member
            }
        }

        logger.LogInformation(
            "Added {Added}/{Requested} demo members to organization {OrgId} ({Admins} Admin, {Members} Member)",
            addedUserIds.Count,
            memberCount,
            organizationId,
            adminsAdded,
            membersAdded);

        return addedUserIds;
    }

    async Task<IReadOnlyList<TeamResponse>> AddDemoTeamsAsync(
        Guid organizationId,
        Guid ownerUserId,
        int teamCount,
        CancellationToken cancellationToken)
    {
        if (teamCount <= 0)
            return [];

        var created = new List<TeamResponse>(teamCount);
        for (var t = 1; t <= teamCount; t++)
        {
            var profile = DemoOrganizationCatalog.GetTeam(t);
            var team = await teams.CreateAsync(
                organizationId,
                new CreateTeamRequest
                {
                    Name = profile.Name,
                    Description = profile.Description
                },
                ownerUserId,
                cancellationToken);
            created.Add(team);
        }

        logger.LogInformation(
            "Created {TeamCount} demo team(s) in organization {OrgId}",
            created.Count,
            organizationId);

        return created;
    }

    async Task PopulateTeamsAsync(
        Guid organizationId,
        Guid ownerUserId,
        IReadOnlyList<TeamResponse> createdTeams,
        IReadOnlyList<Guid> memberUserIds,
        Guid teamLeadRoleId,
        Guid teamMemberRoleId,
        CancellationToken cancellationToken)
    {
        if (createdTeams.Count == 0)
            return;

        var pool = memberUserIds.ToList();
        var assigned = 0;

        for (var i = 0; i < createdTeams.Count; i++)
        {
            var team = createdTeams[i];
            Guid leadUserId;
            string leadTitle;

            if (i == 0)
            {
                leadUserId = ownerUserId;
                leadTitle = "Engineering Manager";
            }
            else if (pool.Count > 0)
            {
                leadUserId = pool[0];
                pool.RemoveAt(0);
                leadTitle = "Team Lead";
            }
            else
            {
                leadUserId = ownerUserId;
                leadTitle = "Team Lead";
            }

            await TryAddTeamMemberAsync(
                organizationId,
                team.Id,
                leadUserId,
                teamLeadRoleId,
                leadTitle,
                ownerUserId,
                cancellationToken);
            assigned++;
        }

        // Round-robin remaining members across teams as Team Member.
        var teamIndex = 0;
        var jobOrdinal = 0;
        foreach (var userId in pool)
        {
            var team = createdTeams[teamIndex % createdTeams.Count];
            await TryAddTeamMemberAsync(
                organizationId,
                team.Id,
                userId,
                teamMemberRoleId,
                DemoOrganizationCatalog.GetJobTitle(jobOrdinal++),
                ownerUserId,
                cancellationToken);
            assigned++;
            teamIndex++;
        }

        logger.LogInformation(
            "Assigned {Assigned} team membership(s) across {TeamCount} team(s) in organization {OrgId}",
            assigned,
            createdTeams.Count,
            organizationId);
    }

    async Task TryAddTeamMemberAsync(
        Guid organizationId,
        Guid teamId,
        Guid userId,
        Guid roleId,
        string? jobTitle,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            await teams.AddMemberAsync(
                organizationId,
                teamId,
                new AddTeamMemberRequest
                {
                    UserId = userId,
                    RoleId = roleId,
                    JobTitle = jobTitle
                },
                actorUserId,
                cancellationToken);
        }
        catch (OrganizationConflictException)
        {
            // already on team
        }
        catch (OrganizationValidationException ex)
        {
            logger.LogWarning(
                ex,
                "Skipped team member {UserId} for team {TeamId} in organization {OrgId}",
                userId,
                teamId,
                organizationId);
        }
    }

    async Task AddPendingInvitationsAsync(
        Guid organizationId,
        Guid ownerUserId,
        Guid memberRoleId,
        Guid? firstTeamId,
        Guid teamMemberRoleId,
        int invitationCount,
        CancellationToken cancellationToken)
    {
        if (invitationCount <= 0)
            return;

        var created = 0;
        for (var i = 0; i < invitationCount; i++)
        {
            var email = DemoOrganizationCatalog.GetInvitationEmail(i);
            var request = new CreateInvitationRequest
            {
                Email = email,
                OrgRoleIds = [memberRoleId]
            };

            // First invitation optionally targets the first team.
            if (i == 0 && firstTeamId is Guid teamId)
            {
                request.TeamId = teamId;
                request.TeamRoleId = teamMemberRoleId;
            }

            try
            {
                await invitations.CreateAsync(organizationId, request, ownerUserId, cancellationToken);
                created++;
            }
            catch (OrganizationConflictException)
            {
                // pending invite already exists
            }
        }

        logger.LogInformation(
            "Created {Created}/{Requested} pending invitation(s) for organization {OrgId}",
            created,
            invitationCount,
            organizationId);
    }

    async Task<AuthUserProfile> ResolveOwnerAsync(string username, CancellationToken cancellationToken)
    {
        var result = await authUsers.ResolveUsersAsync(
            emails: [],
            usernames: [username],
            cancellationToken);

        var owner = result.Users.FirstOrDefault(u =>
            string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        if (owner is null)
        {
            throw new InvalidOperationException(
                $"Owner username '{username}' was not found via auth gRPC. Seed and start team-hub-auth first.");
        }

        return owner;
    }

    async Task<IReadOnlyList<AuthUserProfile>> ResolveUsernamesAsync(
        IReadOnlyList<string> usernames,
        CancellationToken cancellationToken)
    {
        var users = new List<AuthUserProfile>();
        for (var offset = 0; offset < usernames.Count; offset += ResolveBatchSize)
        {
            var batch = usernames.Skip(offset).Take(ResolveBatchSize).ToList();
            var result = await authUsers.ResolveUsersAsync(
                emails: [],
                usernames: batch,
                cancellationToken);
            users.AddRange(result.Users);
        }

        return users;
    }

    static string BuildNip(string nipBase, int index)
    {
        if (!long.TryParse(nipBase, out var value))
            throw new InvalidOperationException($"Seed:NipBase '{nipBase}' is not a valid 10-digit number.");

        var nip = (value + index).ToString();
        if (nip.Length != 10)
            throw new InvalidOperationException($"Computed NIP '{nip}' is not 10 digits; adjust Seed:NipBase.");

        return nip;
    }
}
