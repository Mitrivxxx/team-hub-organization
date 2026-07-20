using Microsoft.AspNetCore.Mvc;
using team_hub_organization.Dtos;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerListTests
{
    [Fact]
    public async Task List_ShouldReturnOnlyCurrentUserOrganizations()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId, name: "Mine", slug: "mine");
        await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, otherUserId, name: "Other", slug: "other");

        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);
        var result = await controller.List(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var organizations = Assert.IsType<List<OrganizationResponse>>(ok.Value);
        Assert.Single(organizations);
        Assert.Equal("Mine", organizations[0].Name);
    }
}
