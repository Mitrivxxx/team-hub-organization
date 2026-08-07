using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using team_hub_organization.Configuration.Options;
using team_hub_organization.Dtos;
using team_hub_organization.Models;
using team_hub_organization.Services.Organizations;
using team_hub_organization.Services.Rbac;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerDeleteTests
{
    [Fact]
    public async Task Delete_WithoutRecentExport_ShouldConflict()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        await Assert.ThrowsAsync<OrganizationConflictException>(() =>
            controller.Delete(organization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_WhenUserIsOwner_WithExport_ShouldSoftDeleteOrganization()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        await OrganizationsControllerTestHelpers.SeedCompletedExportAsync(db, organization.Id, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Delete(organization.Id, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.NotNull(persisted.DeletedAt);
        Assert.Equal(OrganizationStatus.Archived, persisted.Status);
    }

    [Fact]
    public async Task Delete_WhenUserIsNotOwner_ShouldReturnForbidden()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);
        await OrganizationsControllerTestHelpers.SeedCompletedExportAsync(db, organization.Id, ownerId);

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateController(db, memberId);
        await Assert.ThrowsAsync<OrganizationAccessException>(() =>
            controller.Delete(organization.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Restore_WithinRetention_ShouldReactivate()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        await OrganizationsControllerTestHelpers.SeedCompletedExportAsync(db, organization.Id, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        await controller.Delete(organization.Id, CancellationToken.None);
        var restored = await controller.Restore(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(restored);
        var body = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Equal("active", body.Status);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.Null(persisted.DeletedAt);
        Assert.Equal(OrganizationStatus.Active, persisted.Status);
    }

    [Fact]
    public async Task Restore_AfterRetention_ShouldBeGone()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        organization.DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        organization.Status = OrganizationStatus.Archived;
        await db.SaveChangesAsync();

        var service = OrganizationsControllerTestHelpers.CreateOrganizationService(db, retentionDays: 30);
        await Assert.ThrowsAsync<OrganizationGoneException>(() =>
            service.RestoreAsync(organization.Id, userId));
    }

    [Fact]
    public async Task UpdateStatus_Suspended_ShouldBlockMutations()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var statusResult = await controller.UpdateStatus(
            organization.Id,
            new UpdateOrganizationStatusRequest { Status = "suspended" },
            CancellationToken.None);
        Assert.IsType<OkObjectResult>(statusResult);

        await Assert.ThrowsAsync<OrganizationConflictException>(() =>
            controller.Update(organization.Id, new UpdateOrganizationRequest { Name = "Nope" }, CancellationToken.None));
    }

    [Fact]
    public async Task TeamSoftDelete_ShouldRemoveTeamMembers()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);

        var team = new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Eng",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Teams.Add(team);
        db.TeamMembers.Add(new TeamMember
        {
            TeamId = team.Id,
            UserId = userId,
            RoleId = roles.TeamMember.Id,
            JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var teamsController = OrganizationsControllerTestHelpers.CreateTeamsController(db, userId);
        var result = await teamsController.Delete(organization.Id, team.Id, CancellationToken.None);
        Assert.IsType<NoContentResult>(result);
        Assert.Empty(await db.TeamMembers.Where(tm => tm.TeamId == team.Id).ToListAsync());
    }

    [Fact]
    public async Task Purge_ShouldHardDeleteExpiredSoftDeletedOrg()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId);
        organization.DeletedAt = DateTimeOffset.UtcNow.AddDays(-31);
        organization.Status = OrganizationStatus.Archived;
        await db.SaveChangesAsync();

        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IOptions<OrganizationLifecycleOptions>>(
            Options.Create(new OrganizationLifecycleOptions { RetentionDays = 30 }));
        await using var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var purge = new OrganizationPurgeBackgroundService(
            scopeFactory,
            Options.Create(new OrganizationLifecycleOptions { RetentionDays = 30 }));
        await purge.PurgeOnceAsync(CancellationToken.None);

        Assert.False(await db.Organizations.AnyAsync(o => o.Id == organization.Id));
    }
}
