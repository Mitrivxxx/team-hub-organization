using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Tests.Controllers;

public class MembersControllerTests
{
    [Fact]
    public async Task List_WhenMember_ShouldReturnMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, userId);

        var result = await controller.List(organization.Id, roleId: null, teamId: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberResponse>>(ok.Value);
        Assert.Single(members);
        Assert.Equal(userId, members[0].UserId);
    }

    [Fact]
    public async Task List_WhenFilteredByRoleId_ShouldReturnMatchingMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = memberId,
            RoleId = roles.Member.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);

        var result = await controller.List(organization.Id, roleId: roles.Member.Id, teamId: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberResponse>>(ok.Value);
        Assert.Single(members);
        Assert.Equal(memberId, members[0].UserId);
        Assert.Equal(roles.Member.Id, members[0].RoleId);
    }

    [Fact]
    public async Task List_WhenFilteredByTeamId_ShouldReturnTeamMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = memberId,
                RoleId = roles.Member.Id,
                JoinedAt = DateTimeOffset.UtcNow
            },
            new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = otherId,
                RoleId = roles.Member.Id,
                JoinedAt = DateTimeOffset.UtcNow
            });

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Engineering",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Teams.Add(team);
        db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = memberId,
            RoleId = roles.TeamMember.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);

        var result = await controller.List(organization.Id, roleId: null, teamId: team.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberResponse>>(ok.Value);
        Assert.Single(members);
        Assert.Equal(memberId, members[0].UserId);
    }

    [Fact]
    public async Task List_WhenFilteredByRoleAndTeam_ShouldReturnIntersection()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var matchingId = Guid.NewGuid();
        var wrongRoleId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationMembers.AddRange(
            new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = matchingId,
                RoleId = roles.Member.Id,
                JoinedAt = DateTimeOffset.UtcNow
            },
            new OrganizationMember
            {
                OrganizationId = organization.Id,
                UserId = wrongRoleId,
                RoleId = roles.Admin.Id,
                JoinedAt = DateTimeOffset.UtcNow
            });

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Design",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Teams.Add(team);
        db.TeamMembers.AddRange(
            new TeamMember
            {
                TeamId = team.Id,
                UserId = matchingId,
                RoleId = roles.TeamMember.Id,
                JoinedAt = DateTimeOffset.UtcNow
            },
            new TeamMember
            {
                TeamId = team.Id,
                UserId = wrongRoleId,
                RoleId = roles.TeamMember.Id,
                JoinedAt = DateTimeOffset.UtcNow
            });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);

        var result = await controller.List(
            organization.Id,
            roleId: roles.Member.Id,
            teamId: team.Id,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberResponse>>(ok.Value);
        Assert.Single(members);
        Assert.Equal(matchingId, members[0].UserId);
    }

    [Fact]
    public async Task Add_WhenAdmin_ShouldCreateMember()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var newUserId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);

        var result = await controller.Add(organization.Id, new AddMemberRequest
        {
            UserId = newUserId,
            RoleId = roles.Member.Id
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var member = Assert.IsType<MemberResponse>(created.Value);
        Assert.Equal(newUserId, member.UserId);
        Assert.Equal(SystemRoleNames.Member, member.RoleName);
    }
}

public class PermissionsControllerTests
{
    [Fact]
    public async Task List_ShouldReturnCatalog()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        await new PermissionSeedService(db).EnsureCatalogAsync();
        var controller = OrganizationsControllerTestHelpers.CreatePermissionsController(db);

        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = Assert.IsAssignableFrom<IReadOnlyList<PermissionResponse>>(ok.Value);
        Assert.Equal(OrganizationPermissionCodes.All.Count, permissions.Count);
    }
}

public class RolesControllerTests
{
    [Fact]
    public async Task CreateCustomRole_ShouldSucceed()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateRolesController(db, userId);

        var result = await controller.Create(organization.Id, new CreateRoleRequest
        {
            Name = "Billing",
            Scope = "ORG"
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var role = Assert.IsType<RoleResponse>(created.Value);
        Assert.Equal("Billing", role.Name);
        Assert.False(role.IsSystem);
    }
}

public class TeamsControllerTests
{
    [Fact]
    public async Task CreateAndAddMember_ShouldSucceed()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = memberId,
            RoleId = roles.Member.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateTeamsController(db, ownerId);

        var createResult = await controller.Create(organization.Id, new CreateTeamRequest { Name = "Engineering" }, CancellationToken.None);
        var created = Assert.IsType<CreatedAtActionResult>(createResult);
        var team = Assert.IsType<TeamResponse>(created.Value);

        var addResult = await controller.AddMember(organization.Id, team.Id, new AddTeamMemberRequest
        {
            UserId = memberId,
            RoleId = roles.TeamMember.Id
        }, CancellationToken.None);

        var addCreated = Assert.IsType<CreatedAtActionResult>(addResult);
        var teamMember = Assert.IsType<TeamMemberResponse>(addCreated.Value);
        Assert.Equal(memberId, teamMember.UserId);
    }
}

public class InvitationsControllerTests
{
    [Fact]
    public async Task CreateAndAccept_ShouldAddMember()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var inviteeId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        var ownerController = OrganizationsControllerTestHelpers.CreateInvitationsController(db, ownerId);

        var createResult = await ownerController.Create(organization.Id, new CreateInvitationRequest
        {
            Email = "invitee@example.com",
            OrgRoleId = roles.Member.Id
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult);
        var invitation = Assert.IsType<InvitationResponse>(created.Value);
        Assert.NotNull(invitation.Token);

        var inviteeController = OrganizationsControllerTestHelpers.CreateInvitationsController(db, inviteeId);
        var acceptResult = await inviteeController.Accept(invitation.Token!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(acceptResult);
        var member = Assert.IsType<MemberResponse>(ok.Value);
        Assert.Equal(inviteeId, member.UserId);
    }
}

public class MeControllerTests
{
    [Fact]
    public async Task Get_WhenMember_ShouldReturnPermissions()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateMeController(db, userId);

        var result = await controller.Get(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var me = Assert.IsType<MeResponse>(ok.Value);
        Assert.Equal(SystemRoleNames.Owner, me.RoleName);
        Assert.Contains(OrganizationPermissionCodes.OrgDelete, me.Permissions);
    }
}

public class OrganizationsControllerTransferLeaveTests
{
    [Fact]
    public async Task TransferOwnership_ShouldSwapOwnerAndAdmin()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organization.Id,
            UserId = adminId,
            RoleId = roles.Admin.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateController(db, ownerId);
        var result = await controller.TransferOwnership(organization.Id, new TransferOwnershipRequest
        {
            NewOwnerUserId = adminId
        }, CancellationToken.None);

        Assert.IsType<OkResult>(result);

        var meController = OrganizationsControllerTestHelpers.CreateMeController(db, adminId);
        var meResult = await meController.Get(organization.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(meResult);
        var me = Assert.IsType<MeResponse>(ok.Value);
        Assert.Equal(SystemRoleNames.Owner, me.RoleName);
    }

    [Fact]
    public async Task Leave_WhenSoleOwner_ShouldConflict()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, ownerId);

        var result = await controller.Leave(organization.Id, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}
