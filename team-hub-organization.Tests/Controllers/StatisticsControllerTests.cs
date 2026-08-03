using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Tests.Controllers;

public class StatisticsControllerTests
{
    [Fact]
    public async Task Get_WhenMember_ShouldReturnCounts()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        OrganizationsControllerTestHelpers.AddMemberWithRole(db, organization.Id, memberId, roles.Member.Id);

        var now = DateTimeOffset.UtcNow;
        db.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Engineering",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Teams.Add(new Team
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Name = "Deleted",
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = now
        });
        await db.SaveChangesAsync();

        var controller = OrganizationsControllerTestHelpers.CreateStatisticsController(db, ownerId);
        var result = await controller.Get(organization.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var stats = Assert.IsType<OrganizationStatsResponse>(ok.Value);
        Assert.Equal(2, stats.MemberCount);
        Assert.Equal(1, stats.TeamCount);
    }

    [Fact]
    public async Task Get_WhenNonMember_ShouldForbid()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        var controller = OrganizationsControllerTestHelpers.CreateStatisticsController(db, strangerId);
        var result = await controller.Get(organization.Id, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
