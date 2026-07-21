using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using team_hub_organization.Dtos;

namespace team_hub_organization.Tests.Controllers;

public class OrganizationsControllerUpdateTests
{
    [Fact]
    public async Task Update_WhenUserIsMember_ShouldUpdateNameWithoutChangingSlug()
    {
        await using var db = OrganizationsControllerTestHelpers.CreateDbContext();
        var userId = Guid.NewGuid();
        var (organization, _, _) = await OrganizationsControllerTestHelpers.SeedOrganizationAsync(db, userId, slug: "acme");
        var controller = OrganizationsControllerTestHelpers.CreateController(db, userId);

        var result = await controller.Update(organization.Id, new UpdateOrganizationRequest
        {
            Name = "Acme Updated"
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<OrganizationResponse>(ok.Value);
        Assert.Equal("Acme Updated", response.Name);
        Assert.Equal("acme", response.Slug);
        Assert.Null(response.AvatarUrl);

        var persisted = await db.Organizations.SingleAsync(o => o.Id == organization.Id);
        Assert.Equal("acme", persisted.Slug);
    }
}
