using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Data;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Auth;
using team_hub_organization.Services.Members.AllMembers;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;
using team_hub_organization.Services.Teams;

namespace team_hub_organization.Seeding.Internal;

public sealed class OrganizationDemoBuilder(
    OrganizationDbContext db,
    IOrganizationService organizations,
    IMemberService members,
    ITeamService teams,
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

        var nip = BuildNip(seed.NipBase, index);
        var org = await organizations.CreateAsync(
            new CreateOrganizationRequest
            {
                Name = $"Demo Organization {index}",
                Slug = slug,
                Description = "Seeded demo organization",
                Nip = nip,
                Address = new OrganizationAddressDto
                {
                    Country = "Poland",
                    City = "Warsaw",
                    PostalCode = "00-001"
                }
            },
            ownerUserId,
            cancellationToken);

        logger.LogInformation("Created demo organization '{Slug}' ({OrgId})", org.Slug, org.Id);

        var memberRoleId = await db.Roles.AsNoTracking()
            .Where(r => r.OrganizationId == org.Id
                        && r.Scope == RoleScope.Org
                        && r.Name == SystemRoleNames.Member)
            .Select(r => r.Id)
            .FirstAsync(cancellationToken);

        await AddDemoMembersAsync(org.Id, ownerUserId, memberRoleId, seed.MembersPerOrganization, cancellationToken);
        await AddDemoTeamsAsync(org.Id, ownerUserId, seed.TeamsPerOrganization, cancellationToken);
    }

    async Task AddDemoMembersAsync(
        Guid organizationId,
        Guid ownerUserId,
        Guid memberRoleId,
        int memberCount,
        CancellationToken cancellationToken)
    {
        if (memberCount <= 0)
            return;

        var usernames = Enumerable.Range(1, memberCount)
            .Select(n => $"demo{n:D5}")
            .ToList();

        var resolved = await ResolveUsernamesAsync(usernames, cancellationToken);
        if (resolved.Count == 0)
        {
            logger.LogWarning(
                "No demo users resolved for organization {OrgId}; seed auth first (demo00001…)",
                organizationId);
            return;
        }

        var added = 0;
        foreach (var user in resolved)
        {
            if (user.Id == ownerUserId)
                continue;

            try
            {
                await members.AddAsync(
                    organizationId,
                    new AddMemberRequest
                    {
                        UserId = user.Id,
                        RoleIds = [memberRoleId]
                    },
                    ownerUserId,
                    cancellationToken);
                added++;
            }
            catch (OrganizationConflictException)
            {
                // already a member
            }
        }

        logger.LogInformation(
            "Added {Added}/{Requested} demo members to organization {OrgId}",
            added,
            memberCount,
            organizationId);
    }

    async Task AddDemoTeamsAsync(
        Guid organizationId,
        Guid ownerUserId,
        int teamCount,
        CancellationToken cancellationToken)
    {
        if (teamCount <= 0)
            return;

        for (var t = 1; t <= teamCount; t++)
        {
            await teams.CreateAsync(
                organizationId,
                new CreateTeamRequest
                {
                    Name = $"Demo Team {t}",
                    Description = "Seeded demo team"
                },
                ownerUserId,
                cancellationToken);
        }

        logger.LogInformation(
            "Created {TeamCount} demo team(s) in organization {OrgId}",
            teamCount,
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
