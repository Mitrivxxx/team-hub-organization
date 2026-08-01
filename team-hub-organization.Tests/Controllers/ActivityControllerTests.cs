using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Dtos;
using team_hub_organization.Models;

namespace team_hub_organization.Tests.Controllers;

public class ActivityControllerTests
{
    [Fact]
    public async Task List_WhenMember_ShouldReturnPagedActivity()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var (organization, _, _, roles) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        var membersController = OrganizationsControllerTestHelpers.CreateMembersController(db, ownerId);
        await membersController.Add(organization.Id, new AddMemberRequest
        {
            UserId = targetId,
            RoleIds = [roles.Member.Id]
        }, CancellationToken.None);

        var activityController = OrganizationsControllerTestHelpers.CreateActivityController(db, ownerId);
        var result = await activityController.List(
            organization.Id,
            type: ActivityTypes.MemberJoined,
            q: null,
            from: null,
            to: null,
            page: 1,
            pageSize: 20,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<ActivityPageResponse>(ok.Value);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
        Assert.Equal(1, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Equal(ActivityTypes.MemberJoined, page.Items[0].Type);
        Assert.Equal(ownerId, page.Items[0].ActorUserId);
        Assert.Equal(targetId, page.Items[0].TargetUserId);
        Assert.Contains("Member", page.Items[0].Details);
    }

    [Fact]
    public async Task List_WhenNonMember_ShouldForbid()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var strangerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        var activityController = OrganizationsControllerTestHelpers.CreateActivityController(db, strangerId);
        var result = await activityController.List(
            organization.Id,
            type: null,
            q: null,
            from: null,
            to: null,
            page: 1,
            pageSize: 20,
            CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        var objectResult = (ObjectResult)result;
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task List_WhenFilteredByQuery_ShouldMatchDetails()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var ownerId = Guid.NewGuid();
        var (organization, _, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, ownerId);

        db.OrganizationActivities.Add(new OrganizationActivity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Type = ActivityTypes.InvitationSent,
            ActorUserId = ownerId,
            Details = """{"email":"alice@example.com","roles":["Member"]}""",
            OccurredAt = DateTimeOffset.UtcNow
        });
        db.OrganizationActivities.Add(new OrganizationActivity
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            Type = ActivityTypes.TeamCreated,
            ActorUserId = ownerId,
            Details = """{"teamName":"Engineering"}""",
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var activityController = OrganizationsControllerTestHelpers.CreateActivityController(db, ownerId);
        var result = await activityController.List(
            organization.Id,
            type: null,
            q: "alice@",
            from: null,
            to: null,
            page: 1,
            pageSize: 10,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var page = Assert.IsType<ActivityPageResponse>(ok.Value);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(ActivityTypes.InvitationSent, page.Items[0].Type);
    }
}
