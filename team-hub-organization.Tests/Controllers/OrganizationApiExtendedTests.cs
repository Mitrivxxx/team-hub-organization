using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
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
        Assert.Contains(members[0].Roles, r => r.Name == SystemRoleNames.Owner);
    }

    [Fact]
    public async Task List_WhenFilteredByRoleId_ShouldReturnMatchingMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);

        var result = await controller.List(organization.Id, roleId: roles.Member.Id, teamId: null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberResponse>>(ok.Value);
        Assert.Single(members);
        Assert.Equal(memberId, members[0].UserId);
        Assert.Contains(members[0].Roles, r => r.Id == roles.Member.Id);
    }

    [Fact]
    public async Task List_WhenFilteredByTeamId_ShouldReturnTeamMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, otherId, roles.Member.Id);

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

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, matchingId, roles.Member.Id);
        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, wrongRoleId, roles.Admin.Id);

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
            RoleIds = [roles.Member.Id]
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var member = Assert.IsType<MemberResponse>(created.Value);
        Assert.Equal(newUserId, member.UserId);
        Assert.Contains(member.Roles, r => r.Name == SystemRoleNames.Member);
    }
}

public class PermissionsControllerTests
{
    [Fact]
    public async Task List_ShouldReturnOrgCatalog()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreatePermissionsController(db, userId);

        var result = await controller.List(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var permissions = Assert.IsAssignableFrom<IReadOnlyList<PermissionListItemResponse>>(ok.Value);
        Assert.Equal(OrganizationPermissionCodes.All.Count, permissions.Count);
        Assert.All(permissions, p => Assert.Equal(organization.Id, p.OrganizationId));
    }

    [Fact]
    public async Task CreateCustomPermission_ShouldSucceed()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreatePermissionsController(db, userId);

        var result = await controller.Create(organization.Id, new CreatePermissionRequest
        {
            Name = "Billing read",
            Code = "billing.read",
            Description = "Read billing"
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var permission = Assert.IsType<PermissionDetailResponse>(created.Value);
        Assert.Equal("billing.read", permission.Code);
        Assert.False(permission.IsSystem);
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
            Description = "Billing managers",
            Scope = "ORG"
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var role = Assert.IsType<RoleDetailResponse>(created.Value);
        Assert.Equal("Billing", role.Name);
        Assert.Equal("Billing managers", role.Description);
        Assert.False(role.IsSystem);
    }

    [Fact]
    public async Task AssignAndListRoleMembers_ShouldSucceed()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateRolesController(db, ownerId);
        var assignResult = await controller.AssignMember(
            organization.Id,
            roles.Admin.Id,
            new AssignRoleMemberRequest { UserId = memberId },
            CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(assignResult);

        var listResult = await controller.ListMembers(organization.Id, roles.Admin.Id, CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(listResult);
        var members = Assert.IsAssignableFrom<IReadOnlyList<MemberSummaryDto>>(ok.Value);
        Assert.Contains(members, m => m.UserId == memberId);
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

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
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
            OrgRoleIds = [roles.Member.Id]
        }, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(createResult);
        var invitation = Assert.IsType<InvitationResponse>(created.Value);
        Assert.NotNull(invitation.Token);
        Assert.Equal([roles.Member.Id], invitation.OrgRoleIds);

        var inviteeController = OrganizationsControllerTestHelpers.CreateInvitationsController(db, inviteeId);
        var acceptResult = await inviteeController.Accept(invitation.Token!, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(acceptResult);
        var member = Assert.IsType<MemberResponse>(ok.Value);
        Assert.Equal(inviteeId, member.UserId);
        Assert.Contains(member.Roles, r => r.Id == roles.Member.Id);
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
        Assert.Contains(me.Roles, r => r.Name == SystemRoleNames.Owner);
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

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, adminId, roles.Admin.Id);
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
        Assert.Contains(me.Roles, r => r.Name == SystemRoleNames.Owner);
    }

    [Fact]
    public async Task Leave_WhenSoleOwner_ShouldConflict()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, ownerId);

        await Assert.ThrowsAsync<OrganizationConflictException>(() =>
            controller.Leave(organization.Id, CancellationToken.None));
    }
}
